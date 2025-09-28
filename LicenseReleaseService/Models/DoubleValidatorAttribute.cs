using System;
using System.ComponentModel.DataAnnotations;

namespace LicenseReleaseService.Models
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class DoubleValidatorAttribute : ValidationAttribute
    {
        public double Minimum { get; set; }
        public double Maximum { get; set; }

        public DoubleValidatorAttribute()
        {
            Minimum = double.MinValue;
            Maximum = double.MaxValue;
        }

        public DoubleValidatorAttribute(double minimum, double maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return true;
            }

            if (value is double doubleValue)
            {
                return doubleValue >= Minimum && doubleValue <= Maximum;
            }

            if (value is float floatValue)
            {
                return floatValue >= Minimum && floatValue <= Maximum;
            }

            if (value is decimal decimalValue)
            {
                return (double)decimalValue >= Minimum && (double)decimalValue <= Maximum;
            }

            if (value is int intValue)
            {
                return intValue >= Minimum && intValue <= Maximum;
            }

            if (value is long longValue)
            {
                return longValue >= Minimum && longValue <= Maximum;
            }

            return false;
        }

        public override string FormatErrorMessage(string name)
        {
            if (Minimum == double.MinValue && Maximum != double.MaxValue)
            {
                return $"The {name} field must be less than or equal to {Maximum}.";
            }
            else if (Minimum != double.MinValue && Maximum == double.MaxValue)
            {
                return $"The {name} field must be greater than or equal to {Minimum}.";
            }
            else if (Minimum != double.MinValue && Maximum != double.MaxValue)
            {
                return $"The {name} field must be between {Minimum} and {Maximum}.";
            }
            else
            {
                return $"The {name} field is not a valid number.";
            }
        }
    }
}