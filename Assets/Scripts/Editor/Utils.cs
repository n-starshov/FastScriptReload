using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FastScriptReload.Editor
{
    // a facade for API changes introduced in Unity 6
    // https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/overview-of-dot-net-in-unity/dotnet-incompatible-api
    public static class Utils
    {
        public static IReadOnlyList<Assembly> GetAssemblies()
        {
            var loadedAssemblies = 
#if UNITY_6000_6_OR_NEWER
                UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
                AppDomain.CurrentDomain.GetAssemblies();
#endif
            return loadedAssemblies;
        }
        
        public static string GetAssemblyLocation(Assembly assembly)
        {
            var assemblyPath =
#if UNITY_6000_6_OR_NEWER
                assembly.GetLoadedAssemblyPath();
#else
                assembly.Location;
#endif
            return assemblyPath;
        }
        
        public static Assembly LoadAssemblyFromPath(string assemblyPath)
        {
            var assembly = 
#if UNITY_6000_6_OR_NEWER
            UnityEngine.Assemblies.CurrentAssemblies.LoadFromPath(assemblyPath);
#else
            Assembly.LoadFrom(assemblyPath);
#endif
            return assembly;
        }
    }
}