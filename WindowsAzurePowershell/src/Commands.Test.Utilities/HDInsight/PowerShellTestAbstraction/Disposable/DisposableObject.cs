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

namespace Microsoft.WindowsAzure.Commands.Test.Utilities.HDInsight.PowerShellTestAbstraction.Disposable
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using Management.HDInsight.Cmdlet.GetAzureHDInsightClusters.Extensions;

    /// <summary>
    ///     Base implementation of a disposable object.
    /// </summary>
    public abstract class DisposableObject : IQueryDisposable
    {
        private InterlockedBoolean disposed = new InterlockedBoolean(false);

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "Altered to be thread-safe(MWP)")]
        public void Dispose()
        {
            if (this.disposed.ExchangeValue(true))
            {
                // already disposed or disposing;
                return;
            }

            this.Dispose(true);

            // Use SupressFinalize in case a subclass
            // of this type implements a finalizer.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///     Use <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up managed resources if disposing
                this.ReleaseManagedResources();
            }

            // Clean up native resources always
            this.ReleaseUnmanagedResources();
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="DisposableObject" /> class.
        /// </summary>
        /// <remarks>
        ///     Releases unmanaged resources and performs other cleanup operations before the
        ///     <see cref="DisposableObject" /> is reclaimed by garbage collection.
        /// </remarks>
        ~DisposableObject()
        {
            this.Dispose(false);
        }

        /// <inheritdoc />
        public bool IsDisposed()
        {
            return this.disposed.GetValue();
        }

        /// <summary>
        ///     Checks the disposed.
        /// </summary>
        protected void CheckDisposed()
        {
            if (this.disposed.GetValue())
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }

        /// <summary>
        ///     Releases the managed resources.
        /// </summary>
        protected virtual void ReleaseManagedResources()
        {
            Type derrivedType = this.GetType();
            FieldInfo[] fields = derrivedType.GetFields();
            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(this);
                if (value.IsNotNull())
                {
                    Type valueType = value.GetType();
                    if (!valueType.IsValueType)
                    {
                        var asDisposable = value as IDisposable;
                        if (asDisposable.IsNotNull())
                        {
                            asDisposable.Dispose();
                        }
                        var asDisposableEnum = value as IEnumerable<IDisposable>;
                        if (asDisposableEnum.IsNotNull())
                        {
                            foreach (IDisposable disposable in asDisposableEnum)
                            {
                                disposable.Dispose();
                            }
                        }
                        field.SetValue(this, null);
                    }
                }
            }
        }

        /// <summary>
        ///     Releases the unmanaged resources.
        /// </summary>
        protected virtual void ReleaseUnmanagedResources()
        {
        }
    }
}
