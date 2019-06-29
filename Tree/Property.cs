﻿using System;
using System.Collections.Generic;
using System.Reflection;

using RobloxFiles.BinaryFormat;
using RobloxFiles.BinaryFormat.Chunks;

using RobloxFiles.DataTypes;
using RobloxFiles.Utility;

namespace RobloxFiles
{
    public enum PropertyType
    {
        Unknown,
        String,
        Bool,
        Int,
        Float,
        Double,
        UDim,
        UDim2,
        Ray,
        Faces,
        Axes,
        BrickColor,
        Color3,
        Vector2,
        Vector3,
        CFrame = 16,
        Quaternion,
        Enum,
        Ref,
        Vector3int16,
        NumberSequence,
        ColorSequence,
        NumberRange,
        Rect,
        PhysicalProperties,
        Color3uint8,
        Int64,
        SharedString,
    }

    public class Property
    {
        public string Name;
        public Instance Instance { get; internal set; }

        public PropertyType Type;

        public string XmlToken = "";
        public byte[] RawBuffer;

        internal object RawValue;
        internal BinaryRobloxFileWriter CurrentWriter;
        
        internal static BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;

        public static IReadOnlyDictionary<Type, PropertyType> Types = new Dictionary<Type, PropertyType>()
        {
            { typeof(Axes),   PropertyType.Axes  },
            { typeof(Faces),  PropertyType.Faces },

            { typeof(int),     PropertyType.Int    },
            { typeof(bool),    PropertyType.Bool   },
            { typeof(long),    PropertyType.Int64  },
            { typeof(float),   PropertyType.Float  },
            { typeof(double),  PropertyType.Double },
            { typeof(string),  PropertyType.String },

            { typeof(Ray),      PropertyType.Ray     },
            { typeof(Rect),     PropertyType.Rect    },
            { typeof(UDim),     PropertyType.UDim    },
            { typeof(UDim2),    PropertyType.UDim2   },
            { typeof(CFrame),   PropertyType.CFrame  },
            { typeof(Color3),   PropertyType.Color3  },
            { typeof(Vector2),  PropertyType.Vector2 },
            { typeof(Vector3),  PropertyType.Vector3 },

            { typeof(BrickColor),      PropertyType.BrickColor     },
            { typeof(Quaternion),      PropertyType.Quaternion     },
            { typeof(NumberRange),     PropertyType.NumberRange    },
            { typeof(SharedString),    PropertyType.SharedString   },
            { typeof(Vector3int16),    PropertyType.Vector3int16   },
            { typeof(ColorSequence),   PropertyType.ColorSequence  },
            { typeof(NumberSequence),  PropertyType.NumberSequence },
            
            { typeof(PhysicalProperties),  PropertyType.PhysicalProperties },
        };

        private void ImproviseRawBuffer()
        {
            if (RawValue is byte[])
            {
                RawBuffer = RawValue as byte[];
                return;
            }
            else if (RawValue is SharedString)
            {
                var sharedString = CastValue<SharedString>();
                RawBuffer = Convert.FromBase64String(sharedString.MD5_Key);
                return;
            }

            switch (Type)
            {
                case PropertyType.Int:
                    RawBuffer = BitConverter.GetBytes((int)Value);
                    break;
                case PropertyType.Int64:
                    RawBuffer = BitConverter.GetBytes((long)Value);
                    break;
                case PropertyType.Bool:
                    RawBuffer = BitConverter.GetBytes((bool)Value);
                    break;
                case PropertyType.Float:
                    RawBuffer = BitConverter.GetBytes((float)Value);
                    break;
                case PropertyType.Double:
                    RawBuffer = BitConverter.GetBytes((double)Value);
                    break;
                //
            }
        }

        private string ImplicitName
        {
            get
            {
                if (Instance != null)
                {
                    Type instType = Instance.GetType();
                    string typeName = instType.Name;

                    if (typeName == Name)
                    {
                        var implicitName = Name + '_';
                        return implicitName;
                    }
                }

                return Name;
            }
        }

        public object Value
        {
            get
            {
                if (Instance != null)
                {
                    if (Name == "Tags")
                    {
                        byte[] data = Instance.SerializedTags;
                        RawValue = data;
                    }
                    else
                    {
                        FieldInfo field = Instance.GetType()
                            .GetField(ImplicitName, BindingFlags);

                        if (field != null)
                        {
                            object value = field.GetValue(Instance);
                            RawValue = value;
                        }
                        else
                        {
                            Console.WriteLine($"RobloxFiles.Property - No defined field for {Instance.ClassName}.{Name}");
                        }
                    }
                }

                return RawValue;
            }
            set
            {
                if (Instance != null)
                {
                    if (Name == "Tags" && value is byte[])
                    {
                        byte[] data = value as byte[];
                        Instance.SerializedTags = data;
                    }
                    else
                    {
                        FieldInfo field = Instance.GetType()
                            .GetField(ImplicitName, BindingFlags);

                        try
                        {
                            field?.SetValue(Instance, value);
                        }
                        catch
                        {
                            Console.WriteLine($"RobloxFiles.Property - Failed to cast value {value} into property {Instance.ClassName}.{Name}");
                        }
                    }
                }

                RawValue = value;
                RawBuffer = null;

                ImproviseRawBuffer();
            }
        }

        public bool HasRawBuffer
        {
            get
            {
                // Improvise what the buffer should be if this is a primitive.
                if (RawBuffer == null && Value != null)
                    ImproviseRawBuffer();

                return (RawBuffer != null);
            }
        }

        public Property(string name = "", PropertyType type = PropertyType.Unknown, Instance instance = null)
        {
            Instance = instance;
            Name = name;
            Type = type;
        }

        public Property(Instance instance, PROP property)
        {
            Instance = instance;
            Name = property.Name;
            Type = property.Type;
        }

        public string GetFullName()
        {
            string result = Name;

            if (Instance != null)
                result = Instance.GetFullName() + '.' + result;

            return result;
        }

        public override string ToString()
        {
            string typeName = Enum.GetName(typeof(PropertyType), Type);
            string valueLabel = (Value != null ? Value.ToString() : "null");

            if (Type == PropertyType.String)
                valueLabel = '"' + valueLabel + '"';

            return string.Join(" ", typeName, Name, '=', valueLabel);
        }

        public T CastValue<T>()
        {
            T result;

            if (Value is T)
                result = (T)Value;
            else
                result = default(T);

            return result;
        }

        internal void WriteValue<T>() where T : struct
        {
            if (CurrentWriter == null)
                throw new Exception("Property.CurrentWriter must be set to use WriteValue<T>");

            T value = CastValue<T>();
            byte[] bytes = BinaryRobloxFileWriter.GetBytes(value);

            CurrentWriter.Write(bytes);
        }
    }
}