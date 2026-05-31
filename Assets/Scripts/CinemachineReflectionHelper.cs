using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

public static class CinemachineReflectionHelper
{
    private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

    /// <summary>
    /// Находит тип по имени во всех загруженных сборках (с кэшированием)
    /// </summary>
    public static Type FindType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        if (_typeCache.TryGetValue(typeName, out Type cachedType))
        {
            return cachedType;
        }

        // 1. Пытаемся получить тип напрямую
        Type type = Type.GetType(typeName);
        if (type != null)
        {
            _typeCache[typeName] = type;
            return type;
        }

        // 2. Ищем в сборках с добавлением стандартных окончаний Cinemachine
        string[] assembliesToTry = new string[] { "Unity.Cinemachine", "Cinemachine", "Unity.Cinemachine.Runtime" };
        foreach (var assemblyName in assembliesToTry)
        {
            type = Type.GetType($"{typeName}, {assemblyName}");
            if (type != null)
            {
                _typeCache[typeName] = type;
                return type;
            }
        }

        // 3. Сканируем все загруженные сборки
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Игнорируем системные сборки для ускорения поиска
            string name = assembly.FullName;
            if (name.StartsWith("System") || name.StartsWith("mscorlib") || name.StartsWith("Microsoft") || name.StartsWith("netstandard"))
            {
                continue;
            }

            try
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    _typeCache[typeName] = type;
                    return type;
                }
            }
            catch { }
        }

        // 4. Последний шанс: поиск по имени класса без пространства имен или по частичному совпадению
        string cleanName = typeName;
        if (typeName.Contains("."))
        {
            cleanName = typeName.Substring(typeName.LastIndexOf('.') + 1);
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string name = assembly.FullName;
            if (name.StartsWith("System") || name.StartsWith("mscorlib") || name.StartsWith("Microsoft") || name.StartsWith("netstandard"))
            {
                continue;
            }

            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == cleanName || t.FullName == typeName)
                    {
                        _typeCache[typeName] = t;
                        return t;
                    }
                }
            }
            catch { }
        }

        // Записываем null в кэш, чтобы не искать повторно
        _typeCache[typeName] = null;
        return null;
    }
}
