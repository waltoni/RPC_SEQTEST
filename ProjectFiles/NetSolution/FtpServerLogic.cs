#region Using directives
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.Core;
using System;
using System.Linq;
using System.Text.RegularExpressions;
#endregion

using System.Net;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

using FubarDev.FtpServer;
using FubarDev.FtpServer.Features;
using FubarDev.FtpServer.FileSystem.DotNet;
using FubarDev.FtpServer.AccountManagement;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.IO;

public class FtpServerLogic : BaseNetLogic
{
    public override void Start()
    {
        ftpServerLogicObjectParametersReader = new FtpServerLogicObjectParametersReader(LogicObject);

        serverAutoResetEvent = new AutoResetEvent(false);
        customMembershipProvider = new CustomMembershipProvider(LogicObject, ftpServerLogicObjectParametersReader);
        certificateUtils = new CertificateUtils(LogicObject);

        ResultMessage = LogicObject.GetVariable("ResultMessage");
    }

    private IUAVariable ResultMessage;

    public override void Stop()
    {
        StopFtpServer();
    }

    [ExportMethod]
    public void StartFtpServer()
    {
        if (ftpServer != null && ftpServer.Status == FtpServiceStatus.Running)
        {
            Log.Error("FtpServerLogic", "Unable to start the FTP server, it is already running");
            ResultMessage.Value = "Unable to start the FTP server, it is already running";
            return;
        }

        ftpServerLongRunningTask = new LongRunningTask(FtpServerLongTask, LogicObject);
        ftpServerLongRunningTask.Start();
    }

    [ExportMethod]
    public void StopFtpServer()
    {
        if (ftpServer != null)
        {
            StopAndResetFtpServer();
            ResetServerLongRunningTask();
        }
    }

    private void StopAndResetFtpServer()
    {
        try
        {
            var activeConnections = ftpServer.Statistics.ActiveConnections;
            if (activeConnections != 0)
            {
                Log.Info("FtpServerLogic", $"Closing {activeConnections} active FTP connection(s)");
                ResultMessage.Value = $"Closing {activeConnections} active FTP connection(s)";
            }
            ftpServer.ConfigureConnection -= FtpServerConfigureConnection;

            ftpServer.StopAsync(CancellationToken.None).Wait();
            ftpServer.GetConnections().ToList().ForEach(connection => connection.Closed -= FtpServerConnectionClosed);

            Log.Info("FtpServerLogic", "FTP server stopped");

            ResultMessage.Value = "FTP server stopped";
        }
        catch (Exception ex)
        {
            Log.Error("FtpServerLogic", $"An exception occurred while stopping the FTP server: {ex.Message}");
            ResultMessage.Value = $"An exception occurred while stopping the FTP server: {ex.Message}";
        }
        finally
        {
            serverAutoResetEvent.Set();

            ftpServer?.Dispose();
            ftpServer = null;
        }
    }

    private void ResetServerLongRunningTask()
    {
        ftpServerLongRunningTask?.Dispose();
        ftpServerLongRunningTask = null;
    }

    private void FtpServerLongTask()
    {
        if (ftpServerLogicObjectParametersReader == null || !ftpServerLogicObjectParametersReader.AreFtpServerParametersValid())
        {
            Log.Error("FtpServerLogic", "Unable to start the FTP server, one or more parameters are invalid");

            ResultMessage.Value = "Unable to start the FTP server, one or more parameters are invalid";
            return;
        }

        var services = ConfigureFtpServer();
        if (services == null)
            return;

        ftpServer = InitializeAndStartFtpServer(services);
        if (ftpServer == null)
            return;

        serverAutoResetEvent.WaitOne();
    }

    private ServiceCollection ConfigureFtpServer()
    {
        var services = new ServiceCollection();

        // NOTE: It is not possible to read/write model variables inside threads not owned by FactoryTalkOptix
        // For example actions configured on services are invoked on an external thread (internally handled by the FTP server)
        // For this reason is important to directly pass variable values inside service callbacks, without passing the IUAVariable reference
        var filesystemRootPath = ftpServerLogicObjectParametersReader.FilesystemRoot;
        services.Configure<DotNetFileSystemOptions>(opt =>
        {
            opt.RootPath = filesystemRootPath;
        });

        services.AddSingleton<IMembershipProvider, CustomMembershipProvider>(membershipProvider => customMembershipProvider);

        services.AddFtpServer(ftpServerBuilder =>
        {
            ftpServerBuilder.UseDotNetFileSystem(); // Use the .NET file system functionality
        });

        if (ftpServerLogicObjectParametersReader.UseFtps)
        {
            X509Certificate2 cert = GetCertificate();
            services.Configure<AuthTlsOptions>(cfg =>
            {
                cfg.ServerCertificate = cert;
            });
        }

        try
        {
            var ipAddress = ftpServerLogicObjectParametersReader.IPAddress;
            var port = ftpServerLogicObjectParametersReader.Port;
            var minumumPasvPort = ftpServerLogicObjectParametersReader.MinimumPasvPort;
            var maximumPasvPort = ftpServerLogicObjectParametersReader.MaximumPasvPort;
            services.Configure<FtpServerOptions>(opt =>
            {
                opt.ServerAddress = ipAddress;
                opt.Port = Convert.ToInt32(port);
            });

            services.Configure<FtpConnectionOptions>(opt => { opt.DefaultEncoding = System.Text.Encoding.UTF8; });

            // An FTP server can be configured in Passive and Active mode.
            // With FTP's passive (PASV) mode, transfers and directory listings are performed on a separate network connection to the control connection.
            // The FTP client sends a PASV command to FTP server, which replies (in a PASV reply) with an address and a port selected in the range (PasvMinPort, PasvMaxPort)
            // that will be used to make the transfer or listing.
            // With FTP's active mode the FTP client dictates what port must be used, by sending a PORT command. This command specifies the address and port number the client is listening on.
            // The server, when necessary, uses such information to initiate a connection to the client.
            // PASV mode is more firewall-friendly than active FTP mode, because it requires firewall configuration only on the server side.
            services.Configure<SimplePasvOptions>(opt =>
            {
                opt.PublicAddress = IPAddress.Parse(ipAddress);
                opt.PasvMinPort = Convert.ToInt32(minumumPasvPort);
                opt.PasvMaxPort = Convert.ToInt32(maximumPasvPort);
            });
        }
        catch (Exception ex)
        {
            Log.Info("FtpServerLogic", $"An exception occurred while configuring the FTP server's services: {ex.Message}");

            ResultMessage.Value = $"An exception occurred while configuring the FTP server's services: {ex.Message}";
            return null;
        }

        return services;
    }

    private X509Certificate2 GetCertificate()
    {
        if (!string.IsNullOrEmpty(ftpServerLogicObjectParametersReader.ServerCertificateFile) && !string.IsNullOrEmpty(ftpServerLogicObjectParametersReader.ServerPrivateKeyFile))
        {
            return certificateUtils.GetCertificate(ftpServerLogicObjectParametersReader.ServerCertificateFile, ftpServerLogicObjectParametersReader.ServerPrivateKeyFile);
        }
        else
        {
            if (certificateUtils.ShouldGenerateNewCertificate())
                return new X509Certificate2(certificateUtils.GenerateCertificate());
            else
                return new X509Certificate2(certificateUtils.GetCertificate());
        }
    }

    private FtpServer InitializeAndStartFtpServer(ServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        var ftpServer = serviceProvider.GetRequiredService<IFtpServer>() as FtpServer;

        ftpServer.ConfigureConnection += FtpServerConfigureConnection;

        try
        {
            ftpServer.StartAsync(CancellationToken.None).Wait();
            Log.Info("FtpServerLogic", $"FTP server started, waiting for connections on {ftpServerLogicObjectParametersReader.IPAddress}:{ftpServerLogicObjectParametersReader.Port}");

            ResultMessage.Value = $"FTP server started, waiting for connections on {ftpServerLogicObjectParametersReader.IPAddress}:{ftpServerLogicObjectParametersReader.Port}";

            return ftpServer;
        }
        catch (Exception ex)
        {
            Log.Error("FtpServerLogic", $"An exception occurred while starting the FTP server: {ex.Message}");

            ResultMessage.Value = $"An exception occurred while starting the FTP server: {ex.Message}";
            return null;
        }
    }

    private void FtpServerConfigureConnection(object sender, ConnectionEventArgs e)
    {
        Log.Info("FtpServerLogic", $"FTP client {e.Connection.RemoteEndPoint} is trying to connect to the FTP server");

        ResultMessage.Value = $"FTP client {e.Connection.RemoteEndPoint} is trying to connect to the FTP server";
        e.Connection.Closed += FtpServerConnectionClosed;
    }

    private void FtpServerConnectionClosed(object sender, EventArgs e)
    {
        var connectionClosed = sender as FtpConnection;
        if (connectionClosed == null)
        {
            Log.Warning("FtpServerLogic", "Unable to retrieve information about closing connection");

            ResultMessage.Value = "Unable to retrieve information about closing connection";
            return;
        }

        var connectionFeature = connectionClosed.Features.Get<IConnectionFeature>();
        if (connectionFeature == null)
            return;

        Log.Info("FtpServerLogic", $"FTP client {connectionFeature.RemoteEndPoint} disconnected from the FTP server");

        ResultMessage.Value = $"FTP client {connectionFeature.RemoteEndPoint} disconnected from the FTP server";
    }

    #region User validation

    private class CustomMembershipProvider : IMembershipProvider
    {
        //private IUAVariable ResultMessage;
        public CustomMembershipProvider(IUANode logicObject, FtpServerLogicObjectParametersReader logicObjectParametersReader)
        {
            this.logicObject = logicObject;
            this.logicObjectParametersReader = logicObjectParametersReader;

            defaultNamespaceIndex = this.logicObject.NodeId.NamespaceIndex;
            sessionHandler = logicObject.Context.Sessions.CurrentSessionHandler;
        }

        public Task<MemberValidationResult> ValidateUserAsync(string username, string password)
        {
            try
            {
                Log.Info("FtpServerLogic", $"User '{username}' wants to connect to the FTP server");
                //ResultMessage.Value = $"User '{username}' wants to connect to the FTP server";

                if (!IsUserAuthorized(username, password))
                {
                    Log.Warning("FtpServerLogic", $"User '{username}' can not connect to the FTP server. Check credentials and whether '{username}' is authorized to connect to the FTP server");
                    return Task.FromResult(new MemberValidationResult(MemberValidationStatus.InvalidLogin));
                }

                var claims = new[]
                {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, username),
                    new Claim(ClaimsIdentity.DefaultRoleClaimType, username),
                    new Claim(ClaimsIdentity.DefaultRoleClaimType, "user"),
                };

                var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "custom"));
                Log.Info("FtpServerLogic", $"User '{username}' has successfully logged in to the FTP server");

                return Task.FromResult(new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, user));
            }
            catch (Exception ex)
            {
                Log.Info("FtpServerLogic", $"An exception occurred while validating user '{username}' : {ex.Message}");
                return Task.FromResult(new MemberValidationResult(MemberValidationStatus.InvalidLogin));
            }
        }

        private bool IsUserAuthorized(string usernameToValidate, string passwordToValidate)
        {
            try
            {
                var authorizedUsername = logicObjectParametersReader.AuthorizedUsername;
                var authorizedPassword = logicObjectParametersReader.AuthorizedPassword;
                if (usernameToValidate == authorizedUsername && passwordToValidate == authorizedPassword)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error("FtpServerLogic", $"An exception occurred while checking user '{usernameToValidate}' credentials: {ex.Message}");
                return false;
            }
        }


        private readonly IUANode logicObject;
        private readonly FtpServerLogicObjectParametersReader logicObjectParametersReader;
        private readonly ISessionHandler sessionHandler;
        private readonly int defaultNamespaceIndex;
    }

    #endregion

    private class FtpServerLogicObjectParametersReader
    {
        public FtpServerLogicObjectParametersReader(IUANode logicObject)
        {
            this.logicObject = logicObject;
            ReadNetLogicParameters();

            if (!AreFtpServerParametersValid())
                throw new CoreConfigurationException("FtpServerLogic: invalid parameter(s) in FtpServerLogic's configuration");
        }

        private void ReadNetLogicParameters()
        {
            ipAddressVariable = logicObject.GetVariable("IPAddress");
            if (ipAddressVariable == null)
                throw new CoreConfigurationException("IPAddress variable not found");

            portVariable = logicObject.GetVariable("Port");
            if (portVariable == null)
                throw new CoreConfigurationException("Port variable not found");

            filesystemRootVariable = logicObject.GetVariable("FilesystemRoot");
            if (filesystemRootVariable == null)
                throw new CoreConfigurationException("FilesystemRoot variable not found");

            minimumPasvPortVariable = logicObject.GetVariable("MinimumPASVPort");
            if (minimumPasvPortVariable == null)
                throw new CoreConfigurationException("MinimumPASVPort variable not found");

            maximumPasvPortVariable = logicObject.GetVariable("MaximumPASVPort");
            if (maximumPasvPortVariable == null)
                throw new CoreConfigurationException("MaximumPASVPort variable not found");

            authorizedUsernameVariable = logicObject.GetVariable("Username");
            if (authorizedUsernameVariable == null)
                throw new CoreConfigurationException("Username variable not found");

            authorizedPasswordVariable = logicObject.GetVariable("Password");
            if (authorizedPasswordVariable == null)
                throw new CoreConfigurationException("Password variable not found");

            serverCertificateFileVariable = logicObject.GetVariable("ServerCertificateFile");
            if (serverCertificateFileVariable == null)
                throw new CoreConfigurationException("ServerCertificateFile variable not found");

            serverPrivateKeyFileVariable = logicObject.GetVariable("ServerPrivateKeyFile");
            if (serverPrivateKeyFileVariable == null)
                throw new CoreConfigurationException("ServerPrivateKeyFile variable not found");

            useFtpsVariable = logicObject.GetVariable("UseFTPS");
            if (useFtpsVariable == null)
                throw new CoreConfigurationException("UseFTPS variable not found");
        }

        private IUAVariable ipAddressVariable;
        public string IPAddress
        {
            get
            {
                UAValue ipAddressValue = ipAddressVariable.Value;
                if (ipAddressValue == null)
                {
                    Log.Error("FtpServerLogic", "IPAddress value not found");
                    return string.Empty;
                }

                string ipAddress = ipAddressValue;
                if (!IsValidIPAddress(ipAddress))
                {
                    Log.Error("FtpServerLogic", $"FTP IP address '{ipAddress}' format is not valid");
                    return string.Empty;
                }

                return ipAddress;
            }
        }

        private IUAVariable portVariable;
        public UInt16 Port
        {
            get
            {
                UAValue ftpPortValue = portVariable.Value;
                if (ftpPortValue == null)
                {
                    Log.Error("FtpServerLogic", "Port value not set");
                    return 0;
                }

                UInt16 port = ftpPortValue;
                if (port == 0)
                    Log.Error("FtpServerLogic", "Port value must be different from 0");

                return port;
            }
        }

        private IUAVariable filesystemRootVariable;
        public string FilesystemRoot
        {
            get
            {
                try
                {
                    UAValue filesystemRootVariableValue = filesystemRootVariable.Value;
                    if (filesystemRootVariableValue == null)
                    {
                        Log.Error("FtpServerLogic", "FilesystemRoot variable value cannot be empty");
                        return string.Empty;
                    }

                    string filesystemRootStringValue = filesystemRootVariableValue;
                    if (filesystemRootStringValue.Contains(".."))
                    {
                        Log.Error("FtpServerLogic", "FilesystemRoot variable value cannot contain a relative path");
                        return string.Empty;
                    }

                    var filesystemRootResourceUri = new ResourceUri(AddNamespacePrefixToFTOptixRuntimeFolder(filesystemRootStringValue));
                    if (!IsValidResourceUriForCurrentPlatform(filesystemRootResourceUri))
                    {
                        Log.Error("FtpServerLogic", "FilesystemRoot is not valid for the current platform");
                        return string.Empty;
                    }

                    return filesystemRootResourceUri.Uri;
                }
                catch (Exception ex)
                {
                    Log.Error("FtpServerLogic", $"An exception occurred while reading the FTP server's filesystem root path: {ex.Message}");
                    return string.Empty;
                }
            }
        }

        private IUAVariable minimumPasvPortVariable;
        public UInt16 MinimumPasvPort
        {
            get
            {
                UAValue minimumPasvPortValue = minimumPasvPortVariable.Value;
                if (minimumPasvPortValue == null)
                {
                    Log.Error("FtpServerLogic", "Minimum passive port value not set");
                    return 0;
                }

                UInt16 minimumPasvPort = minimumPasvPortValue;
                if (minimumPasvPort < 1024)
                {
                    Log.Error("FtpServerLogic", "Minimum passive port cannot be less than 1024");
                    return 0;
                }

                return minimumPasvPort;
            }
        }

        private IUAVariable maximumPasvPortVariable;
        public UInt16 MaximumPasvPort
        {
            get
            {
                UAValue maximumPasvPortValue = maximumPasvPortVariable.Value;
                if (maximumPasvPortValue == null)
                {
                    Log.Error("FtpServerLogic", "MaxNumberOfConnections value not set");
                    return 0;
                }

                UInt16 maximumPasvPort = maximumPasvPortValue;
                if (maximumPasvPort < MinimumPasvPort)
                {
                    Log.Error("FtpServerLogic", "Maximum passive port must be greater than or equal to the minimum passive port");
                    return 0;
                }

                return maximumPasvPort;
            }
        }

        private IUAVariable authorizedUsernameVariable;
        public string AuthorizedUsername
        {
            get
            {
                UAValue authorizedUsernameValue = authorizedUsernameVariable.Value;
                if (authorizedUsernameValue == null)
                {
                    Log.Error("FtpServerLogic", "Username value not set");
                }

                return authorizedUsernameValue;
            }
        }

        private IUAVariable authorizedPasswordVariable;
        public string AuthorizedPassword
        {
            get
            {
                UAValue authorizedPasswordValue = authorizedPasswordVariable.Value;
                if (authorizedPasswordValue == null)
                {
                    Log.Error("FtpServerLogic", "Password value not set");
                }

                return authorizedPasswordValue;
            }
        }

        private IUAVariable serverCertificateFileVariable;
        public string ServerCertificateFile
        {
            get
            {
                try
                {
                    UAValue serverCertificateFileVariableValue = serverCertificateFileVariable.Value;
                    if (!string.IsNullOrEmpty(serverCertificateFileVariableValue))
                    {
                        var serverCertificateFileResourceUri = new ResourceUri(AddNamespacePrefixToFTOptixRuntimeFolder(serverCertificateFileVariableValue));
                        if (!IsValidResourceUriForCurrentPlatform(serverCertificateFileResourceUri))
                        {
                            Log.Error("FtpServerLogic", "Server certificate file is not valid for the current platform");
                            return string.Empty;
                        }

                        return serverCertificateFileResourceUri.Uri;
                    }
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    Log.Error("FtpServerLogic", $"An exception occurred while reading the FTP server's server certificate file: {ex.Message}");
                    return string.Empty;
                }
            }
        }

        private IUAVariable serverPrivateKeyFileVariable;
        public string ServerPrivateKeyFile
        {
            get
            {
                try
                {
                    UAValue serverPrivateKeyFileVariableValue = serverPrivateKeyFileVariable.Value;
                    if (!string.IsNullOrEmpty(serverPrivateKeyFileVariableValue))
                    {
                        var serverPrivateKeyFileResourceUri = new ResourceUri(AddNamespacePrefixToFTOptixRuntimeFolder(serverPrivateKeyFileVariableValue));
                        if (!IsValidResourceUriForCurrentPlatform(serverPrivateKeyFileResourceUri))
                        {
                            Log.Error("FtpServerLogic", "Server certificate file is not valid for the current platform");
                            return string.Empty;
                        }

                        return serverPrivateKeyFileResourceUri.Uri;
                    }
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    Log.Error("FtpServerLogic", $"An exception occurred while reading the FTP server's server certificate file: {ex.Message}");
                    return string.Empty;
                }
            }
        }

        private IUAVariable useFtpsVariable;
        public bool UseFtps
        {
            get
            {
                UAValue useFtpsValue = useFtpsVariable.Value;
                if (useFtpsValue == null)
                {
                    Log.Error("FtpServerLogic", "UseFtps value not set");
                    return false;
                }

                return (bool)useFtpsValue;
            }
        }

        public bool AreFtpServerParametersValid()
        {
            if (string.IsNullOrEmpty(IPAddress))
                return false;

            if (Port == 0)
                return false;

            if (string.IsNullOrEmpty(FilesystemRoot))
                return false;

            if (MinimumPasvPort == 0 || MaximumPasvPort == 0)
                return false;

            if (MaximumPasvPort <= MinimumPasvPort)
                return false;

            if (AuthorizedUsername == null)
                return false;

            if (AuthorizedPassword == null)
                return false;

            if ((string.IsNullOrEmpty(ServerCertificateFile) && !string.IsNullOrEmpty(ServerCertificateFile)) ||
                (!string.IsNullOrEmpty(ServerCertificateFile) && string.IsNullOrEmpty(ServerCertificateFile)))
                Log.Error("FtpServerLogic", $"Specify both server certificate file and server private key file");

            if (!string.IsNullOrEmpty(ServerCertificateFile) && !File.Exists(ServerCertificateFile))
                Log.Error("FtpServerLogic", $"Server certificate file {ServerCertificateFile} not exists");

            if (!string.IsNullOrEmpty(ServerPrivateKeyFile) && !File.Exists(ServerPrivateKeyFile))
                Log.Error("FtpServerLogic", $"Server private key file {ServerPrivateKeyFile} not exists");

            return true;
        }

        public string AddNamespacePrefixToFTOptixRuntimeFolder(string resourceUriString)
        {
            if (resourceUriString.StartsWith("%APPLICATIONDIR") || resourceUriString.StartsWith("%PROJECTDIR"))
                resourceUriString = $"ns={logicObject.NodeId.NamespaceIndex};{resourceUriString}";

            return resourceUriString;
        }

        private bool IsValidResourceUriForCurrentPlatform(ResourceUri filesystemRootResourceUri)
        {
            if (PlatformCheckerHelper.IsLinuxAsemARM())
            {
                return filesystemRootResourceUri.UriType == UriType.ApplicationRelative ||
                    filesystemRootResourceUri.UriType == UriType.ProjectRelative ||
                    filesystemRootResourceUri.UriType == UriType.USBRelative;
            }

            // Windows/Debian
            return filesystemRootResourceUri.UriType != UriType.Uri;
        }

        private bool IsValidIPAddress(string ipAddress)
        {
            return !string.IsNullOrEmpty(ipAddress) &&
                Regex.IsMatch(ipAddress, "^(([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])\\.){3}([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])$");
        }

        private readonly IUANode logicObject;
    }

    #region Platform helper classes
    private static class PlatformCheckerHelper
    {
        public static bool IsLinuxAsemARM()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
                return false;

            if (machineInfo != MachineInfo.Undefined)
                return machineInfo == MachineInfo.ArmAsem;

            string architecture;
            try
            {
                architecture = LaunchProcess("uname", "-m");
            }
            catch (Exception exception)
            {
                Log.Error("FtpServerLogic", $"Unable to determine architecture: {exception.Message}");
                return false;
            }

            if (architecture.StartsWith("arm", StringComparison.InvariantCultureIgnoreCase))
                machineInfo = MachineInfo.ArmAsem;
            else
                machineInfo = MachineInfo.Other;

            return machineInfo == MachineInfo.ArmAsem;
        }

        private static string LaunchProcess(string processName, string parameter)
        {
            string output;
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = processName,
                UseShellExecute = false,
                Arguments = parameter,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(processStartInfo))
            {
                output = process.StandardOutput.ReadToEnd().Trim();
                bool isSuccess = process.WaitForExit(5000);
                if (!isSuccess)
                    Log.Error("FtpServerLogic", "Command 'uname -m' timed out. It will be terminated");
            }

            return output;
        }

        private enum MachineInfo
        {
            Undefined = 0,
            ArmAsem = 1,
            Other = 2
        }

        private static MachineInfo machineInfo;
    }
    #endregion

    #region Certificate Utils
    public class CertificateUtils
    {
        private string certificateFileName;
        private string keyFileName;

        public CertificateUtils(IUANode logicObject)
        {
            var logicObjectId = logicObject.NodeId.Id.ToString();
            certificateFileName = Path.Combine(Project.Current.ApplicationDirectory, "PKI", "Own", "Certs", $"FtpServerCert{logicObjectId}.der");
            keyFileName = Path.Combine(Project.Current.ApplicationDirectory, "PKI", "Own", "Certs", $"FtpServerKey{logicObjectId}.pem");
        }

        public bool ShouldGenerateNewCertificate()
        {
            return File.Exists(certificateFileName) ? false : true;
        }

        public X509Certificate2 GetCertificate(string certificateFile = null, string keyFile = null)
        {
            if (string.IsNullOrEmpty(certificateFile))
                certificateFile = certificateFileName;
            if (string.IsNullOrEmpty(keyFile))
                keyFile = keyFileName;

            using (RSA rsaPrivateKey = RSA.Create())
            {
                var pemPrivateKeyFile = File.ReadAllText(keyFile).ToCharArray();
                rsaPrivateKey.ImportFromPem(pemPrivateKeyFile);

                using (X509Certificate2 certificate = new X509Certificate2(certificateFile))
                using (X509Certificate2 certificateWithPrivateKey = certificate.CopyWithPrivateKey(rsaPrivateKey))
                {

                    return new X509Certificate2(certificateWithPrivateKey.Export(X509ContentType.Pfx));
                }
            }
        }


        public X509Certificate2 GenerateCertificate()
        {
            using (var rsa = RSA.Create(4096))
            {
                CertificateRequest req = new CertificateRequest(
                    "CN=FTPServer",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.8") }, true));

                req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

                Save(keyFileName, rsa.ExportRSAPrivateKey(), "-----BEGIN RSA PRIVATE KEY-----\r\n", "\r\n-----END RSA PRIVATE KEY-----");

                using (var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365)))
                {
                    Save(certificateFileName, cert.Export(X509ContentType.Cert), "-----BEGIN CERTIFICATE-----\r\n", "\r\n-----END CERTIFICATE-----");
                    return GetCertificate();
                }
            }
        }

        private void Save(string fileName, byte[] certificateRaw, string header, string footer)
        {
            var filePath = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);
            File.WriteAllText(fileName, $"{header}{Convert.ToBase64String(certificateRaw, Base64FormattingOptions.InsertLineBreaks)}{footer}");
        }
    }
    #endregion

    private FtpServerLogicObjectParametersReader ftpServerLogicObjectParametersReader;
    private LongRunningTask ftpServerLongRunningTask;
    private AutoResetEvent serverAutoResetEvent;

    // FTP server library
    private FtpServer ftpServer;
    private CustomMembershipProvider customMembershipProvider;
    private CertificateUtils certificateUtils;
}
