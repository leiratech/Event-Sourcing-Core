using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Leira.EventSourcing.Extensions
{
    internal static class ObjectsExtensions
    {
        public static IEnumerable<MethodInfo> GetMethodsThroughHierarchy(this Type type)
        {
            IEnumerable<MethodInfo> methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (type.BaseType != null)
            {
                methods = methods.Concat(GetMethodsThroughHierarchy(type.BaseType));
            }

            return methods;
        }

        internal static JsonSerializer serializer = new JsonSerializer()
        {
            TypeNameHandling = TypeNameHandling.All,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        internal static Stream ToJsonStream(this object input)
        {
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: Encoding.Default, bufferSize: 1024, leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Formatting.None;
                    serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        internal static T FromJsonStream<T>(this Stream stream)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)(stream);
                }

                using (StreamReader sr = new StreamReader(stream))
                {
                    using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                    {
                        return serializer.Deserialize<T>(jsonTextReader);
                    }
                }
            }
        }
    }
}
