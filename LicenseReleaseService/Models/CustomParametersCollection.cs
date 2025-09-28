using System;
using System.Collections;
using System.Collections.Generic;

namespace LicenseReleaseService.Models
{
    public class CustomParametersCollection : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly Dictionary<string, object> _parameters;

        public CustomParametersCollection()
        {
            _parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public object this[string key]
        {
            get
            {
                if (_parameters.TryGetValue(key, out var value))
                {
                    return value;
                }
                return null;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Parameter key cannot be null or whitespace.", nameof(key));
                }
                _parameters[key] = value;
            }
        }

        public int Count => _parameters.Count;

        public void Add(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Parameter key cannot be null or whitespace.", nameof(key));
            }
            _parameters[key] = value;
        }

        public bool Remove(string key)
        {
            return _parameters.Remove(key);
        }

        public bool ContainsKey(string key)
        {
            return _parameters.ContainsKey(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return _parameters.TryGetValue(key, out value);
        }

        public void Clear()
        {
            _parameters.Clear();
        }

        public T GetValue<T>(string key, T defaultValue = default(T))
        {
            if (TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }
    }
}