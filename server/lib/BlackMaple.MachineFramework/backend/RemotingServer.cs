﻿/* Copyright (c) 2018, John Lenz

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of John Lenz, Black Maple Software, SeedTactics,
      nor the names of other contributors may be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

#if SERVE_REMOTING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using BlackMaple.MachineFramework;
using BlackMaple.MachineWatchInterface;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Reflection.Emit;
using System.Reflection;

namespace BlackMaple.MachineWatch
{
    public class RemotingServer : IDisposable
    {
        public readonly FMSImplementation plugin;
        private readonly IStoreSettings settingsServer;
        private readonly RemoteSingletons singletons;

        public class SettingStore : IStoreSettings
        {
            private string _path;
            public SettingStore(string path) { _path = path; }

            public string GetSettings(string ID)
            {
                var f = System.IO.Path.Combine(
                    _path,
                    System.IO.Path.GetFileNameWithoutExtension(ID))
                    + ".json";
                if (System.IO.File.Exists(f))
                    return System.IO.File.ReadAllText(f);
                else
                    return null;
            }

            public void SetSettings(string ID, string settingsJson)
            {
                var f = System.IO.Path.Combine(
                    _path,
                    System.IO.Path.GetFileNameWithoutExtension(ID))
                    + ".json";
                System.IO.File.WriteAllText(f, settingsJson);
            }
        }

        public RemotingServer() {}

        public RemotingServer(FMSImplementation p, string dataDir)
        {
            plugin = p;

            settingsServer = new SettingStore(dataDir);

            AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveEventHandler;

            //Configure .NET Remoting
            if (System.IO.File.Exists(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile))
            {
                RemotingConfiguration.Configure(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile, false);
            }
            if (System.Runtime.Remoting.Channels.ChannelServices.RegisteredChannels.Count() == 0)
            {
                var clientFormatter = new System.Runtime.Remoting.Channels.BinaryClientFormatterSinkProvider();
                var serverFormatter = new System.Runtime.Remoting.Channels.BinaryServerFormatterSinkProvider();
                serverFormatter.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
                var props = new System.Collections.Hashtable();
                props["port"] = 8086;
                System.Runtime.Remoting.Channels.ChannelServices.RegisterChannel(new System.Runtime.Remoting.Channels.Tcp.TcpChannel(props, clientFormatter, serverFormatter), false);

                System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime = TimeSpan.FromMinutes(3);
                System.Runtime.Remoting.Lifetime.LifetimeServices.SponsorshipTimeout = TimeSpan.FromMinutes(2);
                System.Runtime.Remoting.Lifetime.LifetimeServices.RenewOnCallTime = TimeSpan.FromMinutes(1);
                System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseManagerPollTime = TimeSpan.FromSeconds(10);
                System.Runtime.Remoting.RemotingConfiguration.CustomErrorsMode = System.Runtime.Remoting.CustomErrorsModes.Off;
            }

            var jobDb = plugin.Backend.JobDatabase();
            var logDb = plugin.Backend.LogDatabase();
            var inspServer = plugin.Backend.InspectionControl();
            var jobControl = plugin.Backend.JobControl();
            var oldJob = plugin.Backend.OldJobDecrement();

            singletons = new RemoteSingletons();

            singletons.RemoteSingleton(typeof(IJobDatabase),
                                        "JobDB",
                                        jobDb);
            singletons.RemoteSingleton(typeof(ILogDatabase),
                                        "LogDB",
                                        logDb);
            singletons.RemoteSingleton(typeof(IInspectionControl),
                                        "InspectionControl",
                                        inspServer);
            singletons.RemoteSingleton(typeof(IJobControl),
                                        "JobControl",
                                        jobControl);
            singletons.RemoteSingleton(typeof(IOldJobDecrement),
                                        "OldJobDecrement",
                                        oldJob);
            singletons.RemoteSingleton(typeof(IStoreSettings),
                        "Settings",
                        settingsServer);
        }

        private Assembly ResolveEventHandler(object sender, ResolveEventArgs args)
        {
            var assName = args.Name.Split(',')[0];

            /*
             SAIL is compiled against the backend types in the BlackMaple.MachineWatchInterface
            assembly.  Here in the backned, the types are in the MachineFramework assembly,
            but they use identical code.  This hack allows remoting to work, so types from
            MachineWatchInterface get loaded from MachineFramework
            */
            if (assName == "BlackMaple.MachineWatchInterface") {
                assName = "BlackMaple.MachineFramework";
            }

            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies()) {
                if (ass.GetName().Name == assName) {
                    return ass;
                }
            }

            return null;
        }

#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            if (!disposedValue)
            {
                if (singletons != null) singletons.Disconnect();
                GC.Collect();
                disposedValue = true;
            }
        }
#endregion

        public class RemoteSingletons
        {
            public class WrapperBase : MarshalByRefObject
            {
                internal protected object src = null;

                public override object InitializeLifetimeService()
                {
                    ILease lease = (ILease)base.InitializeLifetimeService();
                    if (lease.CurrentState == LeaseState.Initial) {
                        lease.InitialLeaseTime = TimeSpan.Zero;
                    }
                    return null;
                }

                public object LoadObject()
                {
                    if (src == null)
                        throw new NotImplementedException();
                    return src;
                }
            }

            private static Type BuildWrapperType(ModuleBuilder module, Type iFace)
            {
                var proxy = module.DefineType("Wrap" + iFace.Name + DateTime.UtcNow.Ticks.ToString(),
                                            TypeAttributes.NotPublic | TypeAttributes.Sealed,
                                            typeof(WrapperBase),
                                            new Type[] {iFace});
                var loadFunc = typeof(WrapperBase).GetMethod("LoadObject");

                foreach (MethodInfo method in iFace.GetMethods()) {
                    var parameters = method.GetParameters();
                    var paramTypes = new Type[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                        paramTypes[i] = parameters[i].ParameterType;

                    var methodBuilder = proxy.DefineMethod(method.Name,
                                                        MethodAttributes.Public | MethodAttributes.Virtual,
                                                        method.ReturnType,
                                                        paramTypes);

                    var ilGen = methodBuilder.GetILGenerator();
                    ilGen.Emit(OpCodes.Ldarg_0);
                    ilGen.Emit(OpCodes.Call, loadFunc);
                    for (int i = 1; i < parameters.Length + 1; i++)
                        ilGen.Emit(OpCodes.Ldarg, i);
                    if (method.IsVirtual)
                        ilGen.Emit(OpCodes.Callvirt, method);
                    else
                        ilGen.Emit(OpCodes.Call, method);
                    ilGen.Emit(OpCodes.Ret);

                    proxy.DefineMethodOverride(methodBuilder, method);
                }

                return proxy.CreateType();
            }

            public RemoteSingletons()
            {
                var domain = System.Threading.Thread.GetDomain();
                var builder = domain.DefineDynamicAssembly(new AssemblyName("Wrappers"),
                                                        AssemblyBuilderAccess.Run);
                module = builder.DefineDynamicModule("WrapperModule", false);
                marshaledObjects = new List<WrapperBase>();
            }

            public void RemoteSingleton(Type iFace, string uri, object obj)
            {
                var newType = BuildWrapperType(module, iFace);
                WrapperBase newObj = (WrapperBase)Activator.CreateInstance(newType);
                newObj.src = obj;

                RemotingServices.Marshal(newObj, uri, iFace);

                marshaledObjects.Add(newObj);
            }

            public void Disconnect()
            {
                foreach (var obj in marshaledObjects)
                    RemotingServices.Disconnect(obj);
                marshaledObjects.Clear();
            }

            private ModuleBuilder module;
            private IList<WrapperBase> marshaledObjects;
        }
    }
}

#endif