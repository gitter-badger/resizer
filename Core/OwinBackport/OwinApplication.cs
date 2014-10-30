// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ImageResizer.OwinBackport
{
    internal static class OwinApplication
    {
        private static ShutdownDetector _detector;

        internal static CancellationToken ShutdownToken
        {
            get { return LazyInitializer.EnsureInitialized(ref _detector, InitShutdownDetector).Token; }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "Only cleaned up on shutdown")]
        private static ShutdownDetector InitShutdownDetector()
        {
            var detector = new ShutdownDetector();
            detector.Initialize();
            return detector;
        }
    }
}
