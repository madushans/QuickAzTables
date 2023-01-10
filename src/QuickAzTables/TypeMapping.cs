using Azure.Data.Tables;
using Newtonsoft.Json;
using System.Reflection;

namespace QuickAzTables
{
    internal static class TypeMapping
    {
        private static string TablePropertyNameForObjectProperty(string propertyName) => $"__jsonFor_{propertyName}";

        private static bool IsNativeSupportedType(Type type) =>
            type == typeof(byte[]) ||
            type == typeof(bool) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(double) ||
            type == typeof(Guid) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(string);

        internal static void MapToTableEntity(object item, TableEntity entity)
        {
            //var properties = TableEntity.Flatten(item, null);
            foreach (PropertyInfo property in item.GetType().GetProperties())
            {
                // ignore if the getter does not exist or is not public
                if (property.GetGetMethod(nonPublic: false) is null) continue;

                if (IsNativeSupportedType(property.PropertyType))
                {
                    entity.Add(property.Name, property.GetValue(item));
                }
                else
                {
                    // if its an unsupported type, json serialize it.
                    // note that if saved using a subtype of TableEntity
                    // objects may have saved with their own properties and 
                    // this will create a new property with prefix.
                    var propertyName = TablePropertyNameForObjectProperty(property.Name);
                    var value = property.GetValue(item);
                    var serializedValue = JsonConvert.SerializeObject(value);

                    entity.Add(propertyName, serializedValue);
                }
            }
        }

        internal static T MapFromTableEntity<T>(TableEntity entity) where T : class, new()
        {
            var item = new T();

            foreach (var property in typeof(T).GetProperties())
            {
                // ignore if the setter does not exist or is not public
                if (property.GetSetMethod(nonPublic: false) is null) continue;
                if (IsNativeSupportedType(property.PropertyType))
                {
                    var pass = entity.TryGetValue(property.Name, out var value);

                    // if the entity doesn't have a value we skip.
                    // if the value is null we skip (either it is
                    // a value type which already has its default
                    // or a reference type and is null already, but
                    // it likely isnt since IsNativeSupportedType
                    // doesn't allow objects.)
                    if (!pass || value is null) continue;

                    if (property.PropertyType.IsAssignableFrom(value.GetType()))
                    {
                        property.SetValue(item, value);
                    }
                }
                else
                {
                    var key = TablePropertyNameForObjectProperty(property.Name);
                    var pass = entity.TryGetValue(key, out var parsed);

                    // if the entity doesn't have a value we skip.
                    // if the value is null we skip (either it is
                    // a value type which already has its default
                    // or a reference type and is null already)
                    if (!pass || parsed is null) continue;

                    if (parsed is not string json)
                        throw new Exception($"value for property {property.Name} (table key {key}) " +
                            $"expected to be a string but found {parsed.GetType().FullName}");

                    var value = JsonConvert.DeserializeObject(json, property.PropertyType);

                    property.SetValue(item, value);
                }
            }
            return item;
        }
    }
}