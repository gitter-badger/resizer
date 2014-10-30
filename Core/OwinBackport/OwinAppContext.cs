// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Routing;

namespace ImageResizer.OwinBackport
{
    using ImageResizer.OwinBackport.CallEnvironment;
    using ImageResizer.OwinBackport.DataProtection;
    using ImageResizer.OwinBackport.Infrastructure;
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using AppBuilderDelegate = Func<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>;


    internal partial class OwinAppContext
    {
        private const string TraceName = "ImageResizer.OwinBackport.OwinAppContext";

        private readonly ITrace _trace;

        private bool _detectWebSocketSupportStageTwoExecuted;
        private object _detectWebSocketSupportStageTwoLock;

        public OwinAppContext()
        {
            _trace = TraceFactory.Create(TraceName);
            AppName = HostingEnvironment.SiteName + HostingEnvironment.ApplicationID;
            if (string.IsNullOrWhiteSpace(AppName))
            {
                AppName = Guid.NewGuid().ToString();
            }
        }

        internal IDictionary<string, object> Capabilities { get; private set; }

        internal bool WebSocketSupport { get; set; }
        internal AppFunc AppFunc { get; set; }
        internal string AppName { get; private set; }

        internal void Initialize(AppBuilderDelegate app)
        {
            Capabilities = new ConcurrentDictionary<string, object>();

            var Properties = new ConcurrentDictionary<string, object>();

            Properties[Constants.OwinVersionKey] = Constants.OwinVersion;
            Properties[Constants.HostTraceOutputKey] = TraceTextWriter.Instance;
            Properties[Constants.HostAppNameKey] = AppName;
            Properties[Constants.HostOnAppDisposingKey] = OwinApplication.ShutdownToken;
            Properties[Constants.HostReferencedAssemblies] = new ReferencedAssembliesWrapper();
            Properties[Constants.ServerCapabilitiesKey] = Capabilities;
            Properties[Constants.SecurityDataProtectionProvider] = new MachineKeyDataProtectionProvider().ToOwinFunction();
            
            Capabilities[Constants.SendFileVersionKey] = Constants.SendFileVersion;

            CompilationSection compilationSection = (CompilationSection)System.Configuration.ConfigurationManager.GetSection(@"system.web/compilation");
            bool isDebugEnabled = compilationSection.Debug;
            if (isDebugEnabled)
            {
                Properties[Constants.HostAppModeKey] = Constants.AppModeDevelopment;
            }

            
            try
            {
                AppFunc = app(Properties);
            }
            catch (Exception ex)
            {
                _trace.WriteError(Resources.Trace_EntryPointException, ex);
                throw;
            }

        }

        public OwinCallContext CreateCallContext(
            RequestContext requestContext,
            string requestPathBase,
            string requestPath,
            AsyncCallback callback,
            object extraData)
        {
            return new OwinCallContext(this, requestContext, requestPathBase, requestPath, callback, extraData);
        }

        

    }
}
