﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Duality
{
    /// <summary>
    /// Manages a set of <see cref="GameObject">GameObject</see> and exposes suitable object enumerations as well as un/registeration events.
    /// If a registered object has been disposed, it will be automatically unregistered.
    /// </summary>
    public class GameObjectManager
    {
        private HashSet<GameObject> allObj = new HashSet<GameObject>();


        /// <summary>
        /// [GET] The number of registered objects.
        /// </summary>
        public int Count
        {
            get { return this.allObj.Count; }
        }
        /// <summary>
        /// [GET] Enumerates all registered GameObjects.
        /// </summary>
        public IEnumerable<GameObject> AllObjects
        {
            get
            {
                return this.allObj.Where(o => !o.Disposed);
            }
        }
        /// <summary>
        /// [GET] Enumerates all registered GameObjects that are currently active.
        /// </summary>
        public IEnumerable<GameObject> ActiveObjects
        {
            get
            {
                return this.allObj.Where(o => o.Active);
            }
        }
        /// <summary>
        /// [GET] Enumerates all root GameObjects, i.e. all GameObjects without a parent object.
        /// </summary>
        public IEnumerable<GameObject> RootObjects
        {
            get
            {
                return this.allObj.Where(o => !o.Disposed && o.Parent == null);
            }
        }
        /// <summary>
        /// [GET] Enumerates all <see cref="RootObjects"/> that are currently active.
        /// </summary>
        public IEnumerable<GameObject> ActiveRootObjects
        {
            get
            {
                return this.allObj.Where(o => o.Parent == null && o.Active);
            }
        }


        /// <summary>
        /// Fired once for every <see cref="GameObject"/> add operation.
        /// </summary>
        public event EventHandler<GameObjectGroupEventArgs> GameObjectsAdded;
        /// <summary>
        /// Fired once for every <see cref="GameObject"/> remove operation.
        /// </summary>
        public event EventHandler<GameObjectGroupEventArgs> GameObjectsRemoved;
        /// <summary>
        /// Fired when a registered GameObjects parent has changed
        /// </summary>
        public event EventHandler<GameObjectParentChangedEventArgs> ParentChanged;
        /// <summary>
        /// Fired when a <see cref="Duality.Component"/> is added to an already registered GameObject.
        /// </summary>
        public event EventHandler<ComponentEventArgs> ComponentAdded;
        /// <summary>
        /// Fired when a <see cref="Duality.Component"/> is removed from an already registered GameObject.
        /// </summary>
        public event EventHandler<ComponentEventArgs> ComponentRemoving;

        /// <summary>
        /// Registers a GameObject and all of its children.
        /// </summary>
        /// <param name="obj"></param>
        public void AddObject(GameObject obj)
        {
            this.AddObjects(new GameObject[] { obj });
        }
        /// <summary>
        /// Registers a set of GameObjects
        /// </summary>
        /// <param name="objEnum"></param>
        public void AddObjects(IEnumerable<GameObject> objEnum)
        {
            List<GameObject> addedObjects = new List<GameObject>();
            foreach (GameObject obj in objEnum) {
                this.AddObjectDeep(obj, addedObjects);
            }
            this.OnObjectsAdded(addedObjects);
        }
        /// <summary>
        /// Unregisters a GameObject and all of its children
        /// </summary>
        /// <param name="obj"></param>
        public void RemoveObject(GameObject obj)
        {
            this.RemoveObjects(new GameObject[] { obj });
        }
        /// <summary>
        /// Unregisters a set of GameObjects
        /// </summary>
        /// <param name="objEnum"></param>
        public void RemoveObjects(IEnumerable<GameObject> objEnum)
        {
            List<GameObject> removedObjects = new List<GameObject>();
            foreach (GameObject obj in objEnum) {
                this.RemoveObjectDeep(obj, removedObjects);
            }
            this.OnObjectsRemoved(removedObjects);
        }
        /// <summary>
        /// Unregisters all GameObjects.
        /// </summary>
        public void Clear()
        {
            this.OnObjectsRemoved(this.allObj.ToList());
            this.allObj.Clear();
        }
        /// <summary>
        /// Unregisters all dead / disposed GameObjects
        /// </summary>
        public void Flush()
        {
            // Determine which objects will be removed due to being disposed
            List<GameObject> removed = new List<GameObject>();
            foreach (GameObject obj in this.allObj) {
                if (obj.Disposed)
                    removed.Add(obj);
            }

            // Remove disposed objects
            this.allObj.RemoveWhere(obj => obj.Disposed);

            // Notify removed objects
            this.OnObjectsRemoved(removed);
        }

        private void AddObjectDeep(GameObject obj, List<GameObject> addedObjects)
        {
            if (this.allObj.Add(obj))
                addedObjects.Add(obj);
            foreach (GameObject child in obj.Children)
                this.AddObjectDeep(child, addedObjects);
        }
        private void RemoveObjectDeep(GameObject obj, List<GameObject> removedObjects)
        {
            foreach (GameObject child in obj.Children)
                this.RemoveObjectDeep(child, removedObjects);
            if (this.allObj.Remove(obj))
                removedObjects.Add(obj);
        }

        private void RegisterEvents(GameObject obj)
        {
            obj.EventParentChanged += this.OnParentChanged;
            obj.EventComponentAdded += this.OnComponentAdded;
            obj.EventComponentRemoving += this.OnComponentRemoving;
        }
        private void UnregisterEvents(GameObject obj)
        {
            obj.EventParentChanged -= this.OnParentChanged;
            obj.EventComponentAdded -= this.OnComponentAdded;
            obj.EventComponentRemoving -= this.OnComponentRemoving;
        }

        private void OnObjectsAdded(List<GameObject> objList)
        {
            if (objList.Count == 0)
                return;

            foreach (GameObject obj in objList) {
                this.RegisterEvents(obj);
            }

            this.GameObjectsAdded?.Invoke(this, new GameObjectGroupEventArgs(objList));
        }
        private void OnObjectsRemoved(List<GameObject> objList)
        {
            if (objList.Count == 0)
                return;

            foreach (GameObject obj in objList) {
                this.UnregisterEvents(obj);
            }

            this.GameObjectsRemoved?.Invoke(this, new GameObjectGroupEventArgs(objList));
        }
        private void OnParentChanged(object sender, GameObjectParentChangedEventArgs e)
        {
            this.ParentChanged?.Invoke(sender, e);
        }
        private void OnComponentAdded(object sender, ComponentEventArgs e)
        {
            this.ComponentAdded?.Invoke(sender, e);
        }
        private void OnComponentRemoving(object sender, ComponentEventArgs e)
        {
            this.ComponentRemoving?.Invoke(sender, e);
        }
    }
}
