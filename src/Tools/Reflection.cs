﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace COMPASS.Tools
{
    public static class Reflection
    {
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

        public static List<string> GetObsoleteProperties(Type type)
        {
            List<string> obsoleteProperties = new();

            // Check each property for the presence of the Obsolete attribute
            PropertyInfo[] properties = type.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                ObsoleteAttribute obsoleteAttribute = Attribute.GetCustomAttribute(property, typeof(ObsoleteAttribute)) as ObsoleteAttribute;
                if (obsoleteAttribute != null)
                {
                    obsoleteProperties.Add(property.Name);
                }
            }

            return obsoleteProperties;
        }
    }
}
