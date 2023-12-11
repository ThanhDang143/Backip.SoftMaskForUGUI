﻿using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Coffee.UISoftMask
{
    /// <summary>
    /// Provides functionality to manage materials.
    /// </summary>
    internal static class MaterialRepository
    {
        private static readonly ObjectRepository<Material> s_Repository =
            new ObjectRepository<Material>(nameof(MaterialRepository));

        public static int count => s_Repository.count;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear()
        {
            s_Repository.Clear();
        }
#endif

        /// <summary>
        /// Retrieves a cached material based on the hash.
        /// </summary>
        public static bool Valid(Hash128 hash, ref Material material)
        {
            Profiler.BeginSample("(SM4UI)[MaterialRegistry] Valid");
            var ret = s_Repository.Valid(hash, material);
            Profiler.EndSample();
            return ret;
        }

        /// <summary>
        /// Adds or retrieves a cached material based on the hash.
        /// </summary>
        public static void Get(Hash128 hash, ref Material material, Func<Material> onCreate)
        {
            Profiler.BeginSample("(SM4UI)[MaterialRegistry] Get");
            s_Repository.Get(hash, ref material, onCreate);
            Profiler.EndSample();
        }

        /// <summary>
        /// Adds or retrieves a cached material based on the hash.
        /// </summary>
        public static void Get<T>(Hash128 hash, ref Material material, Func<T, Material> onCreate, T source)
        {
            Profiler.BeginSample("(SM4UI)[MaterialRegistry] Get");
            s_Repository.Get(hash, ref material, onCreate, source);
            Profiler.EndSample();
        }

        /// <summary>
        /// Removes a soft mask material from the cache.
        /// </summary>
        public static void Release(ref Material material)
        {
            Profiler.BeginSample("(SM4UI)[MaterialRegistry] Release");
            s_Repository.Release(ref material);
            Profiler.EndSample();
        }
    }
}
