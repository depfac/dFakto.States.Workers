using System;
using System.Data;
using System.Globalization;

namespace dFakto.States.Workers.Extensions
{
	public static class UtilExtensions
	{
        public static string[] LineToStringArray(this IDataReader reader)
        {
            string[] toReturn = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    toReturn[i] = null;
                    continue;
                }
                var val = reader.GetValue(i);
                switch (val)
                {
                    case float f:
                        toReturn[i] = f.ToString(CultureInfo.InvariantCulture);
                        break;
                    case double d:
                        toReturn[i] = d.ToString(CultureInfo.InvariantCulture);
                        break;
                    case decimal v:
                        toReturn[i] = v.ToString(CultureInfo.InvariantCulture);
                        break;
                    case DateTime v:
                        toReturn[i] = v.ToString("O");
                        break;
                    default:
                        toReturn[i] = val.ToString();
                        break;
                }
            }
            return toReturn;
        }
    }
}
