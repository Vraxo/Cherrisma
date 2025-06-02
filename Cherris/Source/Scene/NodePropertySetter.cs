using System.Reflection;

namespace Cherris;

public static class NodePropertySetter
{
    private static readonly string[] SpecialProperties = { "type", "name", "path", "children", "Node" };

    public static void SetProperties(Node node, Dictionary<string, object> element, List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        foreach ((string key, object value) in element)
        {
            if (SpecialProperties.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            SetNestedMember(node, key, value, deferredNodeAssignments);
        }
    }

    public static void SetNestedMember(object rootInstance, string fullMemberPath, object value, List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        string[] pathParts = fullMemberPath.Split('/');
        object currentObject = rootInstance; // Start from the root instance

        for (var i = 0; i < pathParts.Length; i++)
        {
            var memberName = pathParts[i];
            var memberInfo = ReflectionUtils.GetMemberInfo(currentObject.GetType(), memberName);
            bool isFinalSegment = i == pathParts.Length - 1;

            if (isFinalSegment)
            {
                // We are at the final part of the path. 'currentObject' is the direct parent of the member to be set.
                // 'memberInfo' is the member itself. 'value' is the value from YAML.

                Type memberType = ReflectionUtils.GetMemberType(memberInfo);

                // If the YAML value is a dictionary, and the target member is a complex object type
                if (value is Dictionary<object, object> dictValue && IsComplexObjectType(memberType))
                {
                    // Get the existing instance of this member from its parent ('currentObject').
                    object? existingMemberInstance = ReflectionUtils.GetMemberValue(currentObject, memberInfo);
                    if (existingMemberInstance == null)
                    {
                        // If the property was null (e.g., not initialized in constructor), create and assign it.
                        existingMemberInstance = Activator.CreateInstance(memberType) ?? throw new InvalidOperationException($"Failed to create instance of {memberType.Name}");
                        ReflectionUtils.SetMemberValue(currentObject, memberInfo, existingMemberInstance);
                    }

                    // Now, iterate through the YAML dictionary ('dictValue') and set properties on 'existingMemberInstance'.
                    // For each key-value pair in 'dictValue', make a recursive call to SetNestedMember.
                    // The 'rootInstance' remains the same (for potential deeper deferrals).
                    // The 'fullMemberPath' for the recursive call is extended with the current sub-key.
                    foreach (KeyValuePair<object, object> entry in dictValue)
                    {
                        string subKey = entry.Key.ToString()!;
                        object subValue = entry.Value;
                        // Construct the full path for this sub-property relative to the rootInstance
                        string subPropertyFullPath = fullMemberPath + "/" + subKey;

                        // Recursive call to set the sub-property.
                        SetNestedMember(rootInstance, subPropertyFullPath, subValue, deferredNodeAssignments);
                    }
                }
                else if (ShouldDeferAssignment(memberType, value))
                {
                    if (rootInstance is Node nodeForDeferral)
                    {
                        deferredNodeAssignments?.Add((nodeForDeferral, fullMemberPath, value));
                    }
                    else
                    {
                        // This case means rootInstance was not a Node, but we are trying to defer.
                        // This can happen if SetNestedMember is called for something like Configuration loading.
                        // Deferral is only relevant for Nodes.
                        if (deferredNodeAssignments != null) // Only log if deferral was expected
                        {
                            Log.Warning($"Cannot defer assignment for non-Node root target: {rootInstance.GetType().Name} for path {fullMemberPath}. This may be normal if loading non-Node configurations.");
                        }
                        // Fallback to direct setting if not a Node context for deferral
                        var convertedNonDeferredValue = ValueConversionUtils.ConvertValue(memberType, value);
                        ReflectionUtils.SetMemberValue(currentObject, memberInfo, convertedNonDeferredValue);
                    }
                }
                else
                {
                    // Primitive, list, enum, or Node path (string) to be converted directly
                    var convertedValue = ValueConversionUtils.ConvertValue(memberType, value);
                    ReflectionUtils.SetMemberValue(currentObject, memberInfo, convertedValue);
                }
                return; // Handled the final segment
            }
            else // Not the final segment, navigate deeper
            {
                object? nextObject = ReflectionUtils.GetMemberValue(currentObject, memberInfo);
                if (nextObject == null)
                {
                    nextObject = ReflectionUtils.CreateMemberInstance(memberInfo);
                    ReflectionUtils.SetMemberValue(currentObject, memberInfo, nextObject);
                }
                currentObject = nextObject;
            }
        }
    }

    private static bool ShouldDeferAssignment(Type memberType, object value)
    {
        return memberType.IsSubclassOf(typeof(Node)) && value is string;
    }

    private static bool IsComplexObjectType(Type type)
    {
        // Types that ValueConversionUtils.ConvertValue handles from non-dictionary sources (string, list, primitive)
        if (type.IsEnum || type == typeof(string) || type.IsPrimitive || type == typeof(decimal))
            return false;

        // Node paths are handled by ShouldDeferAssignment or direct string conversion if not deferred
        if (type.IsSubclassOf(typeof(Node)))
            return false;

        // Resource types loaded from string paths
        if (type == typeof(AudioStream) || type == typeof(Sound) || type == typeof(Animation) || type == typeof(Texture) || type == typeof(Font))
            return false;

        // Types like Vector2, Color, List<T> are converted from YAML lists by ValueConversionUtils
        // If YAML provides a dictionary for these, this method will return true, and they will be populated recursively.
        // This is generally fine, e.g. MyVector: { X:1, Y:2 }
        if (type == typeof(Vector2) || type == typeof(Color)) return false; // if YAML is a list
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return false; // if YAML is a list

        // If it's a class (and not Node/Resource) or a non-primitive, non-enum struct,
        // and the YAML provides a dictionary, we want to populate it recursively.
        if (type.IsClass) return true;

        // For structs (ValueType) that are not primitive, enum, decimal, Vector2, Color
        if (type.IsValueType && !type.IsEnum && !type.IsPrimitive && type != typeof(decimal) && type != typeof(Vector2) && type != typeof(Color))
            return true;

        return false; // Default to not complex if not explicitly class or user-defined struct
    }
}