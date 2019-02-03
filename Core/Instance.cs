﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace RobloxFiles
{
    /// <summary>
    /// Describes an object in Roblox's DataModel hierarchy.
    /// Instances can have sets of properties loaded from *.rbxl/*.rbxm files.
    /// </summary>

    public class Instance
    {
        /// <summary>The ClassName of this Instance.</summary>
        public readonly string ClassName;

        /// <summary>A list of properties that are defined under this Instance.</summary>
        public List<Property> Properties = new List<Property>();
        
        private List<Instance> Children = new List<Instance>();
        private Instance rawParent;

        public string Name => ReadProperty("Name", ClassName);
        public override string ToString() => Name;

        /// <summary>Creates an instance using the provided ClassName.</summary>
        /// <param name="className">The ClassName to use for this Instance.</param>
        public Instance(string className = "Instance")
        {
            ClassName = className;
        }

        /// <summary>Creates an instance using the provided ClassName and Name.</summary>
        /// <param name="className">The ClassName to use for this Instance.</param>
        /// <param name="name">The Name to use for this Instance.</param>
        public Instance(string className = "Instance", string name = "Instance")
        {
            Property propName = new Property();
            propName.Name = "Name";
            propName.Type = PropertyType.String;
            propName.Value = name;
            propName.Instance = this;

            ClassName = className;
            Properties.Add(propName);
        }

        /// <summary>Returns true if this Instance is an ancestor to the provided Instance.</summary>
        /// <param name="descendant">The instance whose descendance will be tested against this Instance.</param>
        public bool IsAncestorOf(Instance descendant)
        {
            while (descendant != null)
            {
                if (descendant == this)
                    return true;

                descendant = descendant.Parent;
            }

            return false;
        }

        /// <summary>Returns true if this Instance is a descendant of the provided Instance.</summary>
        /// <param name="ancestor">The instance whose ancestry will be tested against this Instance.</param>
        public bool IsDescendantOf(Instance ancestor)
        {
            return ancestor.IsAncestorOf(this);
        }

        /// <summary>
        /// The parent of this Instance, or null if the instance is the root of a tree.<para/>
        /// Setting the value of this property will throw an exception if:<para/>
        /// - The value is set to itself.<para/>
        /// - The value is a descendant of the Instance.
        /// </summary>
        public Instance Parent
        {
            get
            {
                return rawParent;
            }
            set
            {
                if (IsAncestorOf(value))
                    throw new Exception("Parent would result in circular reference.");

                if (Parent == this)
                    throw new Exception("Attempt to set parent to self.");

                if (rawParent != null)
                    rawParent.Children.Remove(this);

                value.Children.Add(this);
                rawParent = value;
            }
        }

        /// <summary>
        /// Returns a snapshot of the Instances currently parented to this Instance, as an array.
        /// </summary>
        public Instance[] GetChildren()
        {
            return Children.ToArray();
        }

        /// <summary>
        /// Returns a snapshot of the Instances that are descendants of this Instance, as an array.
        /// </summary>
        public Instance[] GetDescendants()
        {
            Instance[] results = GetChildren();

            foreach (Instance child in results)
            {
                Instance[] childResults = child.GetDescendants();
                results = results.Concat(childResults).ToArray();
            }

            return results;
        }

        /// <summary>
        /// Returns the first child of this Instance whose Name is the provided string name.
        /// If the instance is not found, this returns null.
        /// </summary>
        /// <param name="name">The Name of the Instance to find.</param>
        /// <param name="recursive">Indicates if we should search descendants as well.</param>
        public Instance FindFirstChild(string name, bool recursive = false)
        {
            Instance result = null;

            var query = Children.Where(child => child.Name == name);
            if (query.Count() > 0)
                result = query.First();

            return result;
        }

        /// <summary>
        /// Returns the first Instance whose ClassName is the provided string className. If the instance is not found, this returns null.
        /// </summary>
        /// <param name="className">The ClassName of the Instance to find.</param>
        public Instance FindFirstChildOfClass(string className)
        {
            Instance result = null;

            var query = Children.Where(child => child.ClassName == className);
            if (query.Count() > 0)
                result = query.First();

            return result;
        }

        /// <summary>
        /// Returns a string descrbing the index traversal of this Instance, starting from its root ancestor.
        /// </summary>
        public string GetFullName()
        {
            string fullName = Name;
            Instance at = Parent;

            while (at != null)
            {
                fullName = at.Name + '.' + fullName;
                at = at.Parent;
            }

            return fullName;
        }

        /// <summary>
        /// Looks for a property with the specified property name, and returns it as an object.
        /// <para/>The resulting value may be null if the property is not serialized.
        /// <para/>You can use the templated ReadProperty overload to fetch it as a specific type with a default value provided.
        /// </summary>
        /// <param name="propertyName">The name of the property to be fetched from this Instance.</param>
        /// <returns>An object reference to the value of the specified property, if it exists.</returns>
        /// 
        public object ReadProperty(string propertyName)
        {
            Property property = null;

            var query = Properties.Where((prop) => prop.Name.ToLower() == propertyName.ToLower());
            if (query.Count() > 0)
                property = query.First();

            return (property != null ? property.Value : null);
        }

        /// <summary>
        /// Looks for a property with the specified property name, and returns it as the specified type.<para/>
        /// If it cannot be converted, the provided nullFallback value will be returned instead.
        /// </summary>
        /// <typeparam name="T">The value type to convert to when finding the specified property name.</typeparam>
        /// <param name="propertyName">The name of the property to be fetched from this Instance.</param>
        /// <param name="nullFallback">A fallback value to be returned if casting to T fails, or the property is not found.</param>
        /// <returns></returns>
        public T ReadProperty<T>(string propertyName, T nullFallback)
        {
            try
            {
                object result = ReadProperty(propertyName);
                return (T)result;
            }
            catch
            {
                return nullFallback;
            }
        }

        /// <summary>
        /// Looks for a property with the specified property name. If found, it will try to set the value of the referenced outValue to its value.<para/>
        /// Returns true if the property was found and its value was casted to the referenced outValue.<para/>
        /// If it returns false, the outValue will not have its value set.
        /// </summary>
        /// <typeparam name="T">The value type to convert to when finding the specified property name.</typeparam>
        /// <param name="propertyName">The name of the property to be fetched from this Instance.</param>
        /// <param name="outValue">The value to write to if the property can be casted to T correctly.</param>
        public bool TryReadProperty<T>(string propertyName, ref T outValue)
        {
            try
            {
                object result = ReadProperty(propertyName);
                outValue = (T)result;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds a property by reference to this Instance's property list.
        /// This is used during the file loading procedure.
        /// </summary>
        /// <param name="prop">A reference to the property that will be added.</param>
        public void AddProperty(ref Property prop)
        {
            Properties.Add(prop);
        }

        /// <summary>
        /// Treats the provided string as if you were indexing a specific child or descendant of this Instance.<para/>
        /// The provided string can either be:<para/>
        /// - The name of a child that is parented to this Instance. (  Example: game["Workspace"]  )<para/>
        /// - A period-separated path to a descendant of this Instance. (  Example: game["Workspace.Terrain"]  )<para/>
        /// This will throw an exception if any instance in the traversal is not found.
        /// </summary>
        public Instance this[string accessor]
        {
            get
            {
                Instance result = this;

                foreach (string name in accessor.Split('.'))
                {
                    Instance next = result.FindFirstChild(name);

                    if (next == null)
                        throw new Exception(name + " is not a valid member of " + result.Name);

                    result = next;
                }

                return result;
            }
        }
    }
}