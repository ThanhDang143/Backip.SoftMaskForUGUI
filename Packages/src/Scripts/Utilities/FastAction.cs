using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Coffee.UISoftMask
{
    /// <summary>
    /// Base class for a fast action.
    /// </summary>
    internal class FastActionBase<T>
    {
        private static readonly ObjectPool<LinkedListNode<T>> s_NodePool =
            new ObjectPool<LinkedListNode<T>>(() => new LinkedListNode<T>(default), _ => true, x => x.Value = default);

        private readonly LinkedList<T> _delegates = new LinkedList<T>();

        /// <summary>
        /// Adds a delegate to the action.
        /// </summary>
        public void Add(T rhs)
        {
            Profiler.BeginSample("(SM4UI)[FastAction] Add Action");
            var node = s_NodePool.Rent();
            node.Value = rhs;
            _delegates.AddLast(node);
            Profiler.EndSample();
        }

        /// <summary>
        /// Removes a delegate from the action.
        /// </summary>
        public void Remove(T rhs)
        {
            Profiler.BeginSample("(SM4UI)[FastAction] Remove Action");
            var node = _delegates.Find(rhs);
            if (node != null)
            {
                _delegates.Remove(node);
                s_NodePool.Return(ref node);
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Invokes the action with a callback function.
        /// </summary>
        protected void Invoke(Action<T> callback)
        {
            var node = _delegates.First;
            while (node != null)
            {
                try
                {
                    callback(node.Value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                node = node.Next;
            }
        }
    }

    /// <summary>
    /// A fast action without parameters.
    /// </summary>
    internal class FastAction : FastActionBase<Action>
    {
        /// <summary>
        /// Invoke all the registered delegates.
        /// </summary>
        public void Invoke()
        {
            Invoke(action => action.Invoke());
        }
    }
}
