// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics;

namespace ImageResizer.OwinBackport.Infrastructure
{
    internal interface ITrace
    {
        void Write(TraceEventType eventType, string format, params object[] args);
    }
}
