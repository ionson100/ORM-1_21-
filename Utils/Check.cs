﻿using System;

namespace ORM_1_21_.Utils
{
    internal class Check
    {
        public static T NotNull<T>(T value, string parameterName, Action action = null) where T : class
        {
            if (value == null)
            {
                if (action != null)
                {
                    action.Invoke();
                }
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static void AsTwoValue(double[] values, string name = null)
        {
            if (values.Length != 2)
            {
                throw new Exception($"The array: {name} must consist of two values X and Y");
            }
        }


        public static string NotEmpty(string value, string parameterName, Action action = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (action != null)
                {
                    action.Invoke();
                }
                throw new ArgumentException($"{parameterName}  ArgumentIsNull");
            }

            return value;
        }
    }
}
