// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Compilation;

namespace ImageResizer.OwinBackport.Infrastructure
{
    internal class ReferencedAssembliesWrapper : IEnumerable<Assembly>
    {
        public IEnumerator<Assembly> GetEnumerator()
        {
            return BuildManager.GetReferencedAssemblies().Cast<Assembly>().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
