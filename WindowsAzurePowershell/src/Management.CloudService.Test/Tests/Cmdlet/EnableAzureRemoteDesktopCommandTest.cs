﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.WindowsAzure.Management.CloudService.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security;
    using CloudService.Model;
    using Cmdlet;
    using Extensions;
    using Management.Services;
    using Management.Test.Stubs;
    using Microsoft.WindowsAzure.Management.CloudService.Test.Utilities;
    using Node.Cmdlet;
    using TestData;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Management.Test.Tests.Utilities;

    /// <summary>
    /// Basic unit tests for the Enable-AzureServiceProjectRemoteDesktop enableRDCmdlet.
    /// </summary>
    [TestClass]
    public class EnableAzureRemoteDesktopCommandTest : TestBase
    {
        static private MockCommandRuntime mockCommandRuntime;

        static private EnableAzureServiceProjectRemoteDesktopCommand enableRDCmdlet;

        private AddAzureNodeWebRoleCommand addNodeWebCmdlet;

        private AddAzureNodeWorkerRoleCommand addNodeWorkerCmdlet;

        [TestInitialize]
        public void SetupTest()
        {
            GlobalPathInfo.GlobalSettingsDirectory = Data.AzureSdkAppDir;
            CmdletSubscriptionExtensions.SessionManager = new InMemorySessionManager();
            mockCommandRuntime = new MockCommandRuntime();

            addNodeWebCmdlet = new AddAzureNodeWebRoleCommand();
            addNodeWorkerCmdlet = new AddAzureNodeWorkerRoleCommand();
            enableRDCmdlet = new EnableAzureServiceProjectRemoteDesktopCommand();

            addNodeWorkerCmdlet.CommandRuntime = mockCommandRuntime;
            addNodeWebCmdlet.CommandRuntime = mockCommandRuntime;
            enableRDCmdlet.CommandRuntime = mockCommandRuntime;
        }

        /// <summary>
        /// Invoke the Enable-AzureServiceProjectRemoteDesktop enableRDCmdlet.
        /// </summary>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        public static void EnableRemoteDesktop(string username, string password)
        {
            SecureString securePassword = null;
            if (password != null)
            {
                securePassword = new SecureString();
                foreach (char ch in password)
                {
                    securePassword.AppendChar(ch);
                }
                securePassword.MakeReadOnly();
            }

            if (enableRDCmdlet == null)
            {
                enableRDCmdlet = new EnableAzureServiceProjectRemoteDesktopCommand();
                if (mockCommandRuntime == null)
                {
                    mockCommandRuntime = new MockCommandRuntime();
                }
                enableRDCmdlet.CommandRuntime = mockCommandRuntime;
            }

            enableRDCmdlet.Username = username;
            enableRDCmdlet.Password = securePassword;
            enableRDCmdlet.EnableRemoteDesktop();
        }

        public static void VerifyWebRole(ServiceDefinitionSchema.WebRole role, bool isForwarder)
        {
            Assert.AreEqual(isForwarder ? 1 : 0, role.Imports.Where(i => i.moduleName == "RemoteForwarder").Count());
            Assert.AreEqual(1, role.Imports.Where(i => i.moduleName == "RemoteAccess").Count());
        }

        public static void VerifyWorkerRole(ServiceDefinitionSchema.WorkerRole role, bool isForwarder)
        {
            Assert.AreEqual(isForwarder ? 1 : 0, role.Imports.Where(i => i.moduleName == "RemoteForwarder").Count());
            Assert.AreEqual(1, role.Imports.Where(i => i.moduleName == "RemoteAccess").Count());
        }

        public static void VerifyRoleSettings(AzureService service)
        {
            IEnumerable<ServiceConfigurationSchema.RoleSettings> settings =
                Enumerable.Concat(
                    service.Components.CloudConfig.Role,
                    service.Components.LocalConfig.Role);
            foreach (ServiceConfigurationSchema.RoleSettings roleSettings in settings)
            {
                Assert.AreEqual(
                    1,
                    roleSettings
                        .Certificates
                        .Where(c => c.name == "Microsoft.WindowsAzure.Plugins.RemoteAccess.PasswordEncryption")
                        .Count());
            }
        }
        
        /// <summary>
        /// Perform basic parameter validation.
        /// </summary>
        [TestMethod]
        public void EnableRemoteDesktopBasicParameterValidation()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                files.CreateNewService("NEW_SERVICE");

                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop(null, null));
                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop(string.Empty, string.Empty));
                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop("user", null));
                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop("user", string.Empty));
                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop("user", "short"));
                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop("user", "onlylower"));
                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop("user", "ONLYUPPER"));
                Testing.AssertThrows<ArgumentException>(
                    () => EnableRemoteDesktop("user", "1234567890"));
            }
        }

        /// <summary>
        /// Enable remote desktop for an empty service.
        /// </summary>
        [TestMethod]
        public void EnableRemoteDesktopForEmptyService()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                files.CreateNewService("NEW_SERVICE");
                Testing.AssertThrows<InvalidOperationException>(() =>
                    EnableRemoteDesktop("user", "GoodPassword!"));
            }
        }

        /// <summary>
        /// Enable remote desktop for a simple web role.
        /// </summary>
        [TestMethod]
        public void EnableRemoteDesktopForWebRole()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                addNodeWebCmdlet.AddAzureNodeWebRoleProcess("WebRole", 1, root);
                EnableRemoteDesktop("user", "GoodPassword!");

                // Verify the role has been setup with forwarding, access,
                // and certs
                AzureService service = new AzureService(root, null);
                VerifyWebRole(service.Components.Definition.WebRole[0], true);
                VerifyRoleSettings(service);
            }
        }

        /// <summary>
        /// Enable remote desktop for web and worker roles.
        /// </summary>
        [TestMethod]
        public void EnableRemoteDesktopForWebAndWorkerRoles()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                addNodeWebCmdlet.AddAzureNodeWebRoleProcess("WebRole", 1, root);
                addNodeWorkerCmdlet.AddAzureNodeWorkerRoleProcess("WorkerRole", 1, root);
                mockCommandRuntime.ResetPipelines();
                EnableRemoteDesktop("user", "GoodPassword!");

                // Verify the roles have been setup with forwarding, access,
                // and certs
                AzureService service = new AzureService(root, null);
                VerifyWebRole(service.Components.Definition.WebRole[0], false);
                VerifyWorkerRole(service.Components.Definition.WorkerRole[0], true);
                VerifyRoleSettings(service);
                Assert.AreEqual<int>(0, mockCommandRuntime.OutputPipeline.Count);
            }
        }

        /// <summary>
        /// Enable remote desktop for multiple web and worker roles.
        /// </summary>
        [TestMethod]
        public void EnableRemoteDesktopForMultipleWebAndWorkerRolesTwice()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                files.CreateAzureSdkDirectoryAndImportPublishSettings();
                string root = files.CreateNewService("NEW_SERVICE");
                addNodeWebCmdlet.AddAzureNodeWebRoleProcess("WebRole_1", 1, root);
                addNodeWebCmdlet.AddAzureNodeWebRoleProcess("WebRole_2", 1, root);
                addNodeWorkerCmdlet.AddAzureNodeWorkerRoleProcess("WorkerRole_1", 1, root);
                addNodeWorkerCmdlet.AddAzureNodeWorkerRoleProcess("WorkerRole_2", 1, root);
                mockCommandRuntime.ResetPipelines();
                
                enableRDCmdlet.PassThru = true;
                EnableRemoteDesktop("user", "GoodPassword!");

                enableRDCmdlet.PassThru = false;
                EnableRemoteDesktop("other", "OtherPassword!");

                // Verify the roles have been setup with forwarding, access,
                // and certs
                AzureService service = new AzureService(root, null);
                VerifyWebRole(service.Components.Definition.WebRole[0], false);
                VerifyWebRole(service.Components.Definition.WebRole[0], false);
                VerifyWorkerRole(service.Components.Definition.WorkerRole[0], true);
                VerifyWorkerRole(service.Components.Definition.WorkerRole[1], false);
                VerifyRoleSettings(service);
                Assert.AreEqual<int>(1, mockCommandRuntime.OutputPipeline.Count);
                Assert.IsTrue((bool)mockCommandRuntime.OutputPipeline[0]);
            }
        }
    }
}
