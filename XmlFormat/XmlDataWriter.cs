﻿using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

using RobloxFiles.XmlFormat.PropertyTokens;

namespace RobloxFiles.XmlFormat
{
    public static class XmlDataWriter
    {
        public static XmlWriterSettings Settings = new XmlWriterSettings()
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\r\n",
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = true,
            NamespaceHandling = NamespaceHandling.Default
        };

        private static string CreateReferent()
        {
            Guid referentGuid = Guid.NewGuid();

            string referent = "RBX" + referentGuid
                .ToString()
                .ToUpper();

            return referent.Replace("-", "");
        }

        private static string GetEnumName<T>(T item) where T : struct
        {
            return Enum.GetName(typeof(T), item);
        }

        internal static void RecordInstances(XmlRobloxFile file, Instance inst)
        {
            foreach (Instance child in inst.GetChildren())
                RecordInstances(file, child);

            string referent = CreateReferent();
            file.Instances.Add(referent, inst);
            inst.XmlReferent = referent;
        }

        public static XmlElement CreateRobloxElement(XmlDocument doc)
        {
            XmlElement roblox = doc.CreateElement("roblox");
            doc.AppendChild(roblox);

            roblox.SetAttribute("xmlns:xmime", "http://www.w3.org/2005/05/xmlmime");
            roblox.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            roblox.SetAttribute("xsi:noNamespaceSchemaLocation", "http://www.roblox.com/roblox.xsd");
            roblox.SetAttribute("version", "4");

            XmlElement externalNull = doc.CreateElement("External");
            roblox.AppendChild(externalNull);
            externalNull.InnerText = "null";

            XmlElement externalNil = doc.CreateElement("External");
            roblox.AppendChild(externalNil);
            externalNil.InnerText = "nil";

            return roblox;
        }

        public static XmlNode WriteProperty(Property prop, XmlDocument doc, XmlRobloxFile file)
        {
            string propType = prop.XmlToken;

            if (prop.XmlToken.Length == 0)
            {
                propType = GetEnumName(prop.Type);

                switch (prop.Type)
                {
                    case PropertyType.CFrame:
                    case PropertyType.Quaternion:
                        propType = "CoordinateFrame";
                        break;
                    case PropertyType.Enum:
                        propType = "token";
                        break;
                    case PropertyType.Rect:
                        propType = "Rect2D";
                        break;
                    case PropertyType.Int:
                    case PropertyType.Bool:
                    case PropertyType.Float:
                    case PropertyType.Int64:
                    case PropertyType.Double:
                        propType = propType.ToLower();
                        break;
                    case PropertyType.String:
                        propType = (prop.HasRawBuffer ? "BinaryString" : "string");
                        break;
                }
            }
            
            IXmlPropertyToken handler = XmlPropertyTokens.GetHandler(propType);

            if (handler == null)
            {
                Console.WriteLine("XmlDataWriter.WriteProperty: No token handler found for property type: {0}", propType);
                return null;
            }

            XmlElement propElement = doc.CreateElement(propType);
            propElement.SetAttribute("name", prop.Name);

            XmlNode propNode = propElement;
            handler.WriteProperty(prop, doc, propNode);

            if (propNode.ParentNode != null)
            {
                XmlNode newNode = propNode.ParentNode;
                newNode.RemoveChild(propNode);
                propNode = newNode;
            }

            if (prop.Type == PropertyType.SharedString)
            {
                string data = prop.Value.ToString();
                byte[] buffer = Convert.FromBase64String(data);

                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(buffer);
                    string key = Convert.ToBase64String(hash);

                    if (!file.SharedStrings.ContainsKey(key))
                        file.SharedStrings.Add(key, data);

                    propNode.InnerText = key;
                }
            }

            return propNode;
        }

        public static XmlNode WriteInstance(Instance instance, XmlDocument doc, XmlRobloxFile file)
        {
            XmlElement instNode = doc.CreateElement("Item");
            instNode.SetAttribute("class", instance.ClassName);
            instNode.SetAttribute("referent", instance.XmlReferent);

            XmlElement propsNode = doc.CreateElement("Properties");
            instNode.AppendChild(propsNode);

            var props = instance.Properties;

            foreach (string propName in props.Keys)
            {
                Property prop = props[propName];
                XmlNode propNode = WriteProperty(prop, doc, file);
                propsNode.AppendChild(propNode);
            }

            foreach (Instance child in instance.GetChildren())
            {
                XmlNode childNode = WriteInstance(child, doc, file);
                instNode.AppendChild(childNode);
            }

            return instNode;
        }

        public static XmlNode WriteSharedStrings(XmlDocument doc, XmlRobloxFile file)
        {
            XmlElement sharedStrings = doc.CreateElement("SharedStrings");

            var binaryWriter = XmlPropertyTokens.GetHandler<BinaryStringToken>();
            var bufferProp = new Property("SharedString", PropertyType.String);

            foreach (string md5 in file.SharedStrings.Keys)
            {
                XmlElement sharedString = doc.CreateElement("SharedString");
                sharedString.SetAttribute("md5", md5);

                string data = file.SharedStrings[md5];
                byte[] buffer = Convert.FromBase64String(data);

                bufferProp.SetRawBuffer(buffer);
                binaryWriter.WriteProperty(bufferProp, doc, sharedString);

                sharedStrings.AppendChild(sharedString);
            }

            return sharedStrings;
        }
    }
}
