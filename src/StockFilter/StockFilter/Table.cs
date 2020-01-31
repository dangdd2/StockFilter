using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StockFilter
{
    public class Table
    {
        public string LocationId { get; set; }

        private readonly Dictionary<string, string> _columnDataMapper = new Dictionary<string, string>();

        private readonly Dictionary<Type, string> _dataMapper = new Dictionary<Type, string>
        {
            {typeof(int), "BIGINT"},
            {typeof(int?), "BIGINT"},
            {typeof(string), "NVARCHAR(1000)"},
            {typeof(bool), "BIT"},
            {typeof(bool?), "BIT"},
            {typeof(DateTime), "DATETIME"},
            {typeof(DateTime?), "DATETIME"},
            {typeof(float), "FLOAT"},
            {typeof(float?), "FLOAT"},
            {typeof(decimal), "DECIMAL(18,3)"},
            {typeof(decimal?), "DECIMAL(18,3)"},
            {typeof(Guid), "UNIQUEIDENTIFIER"},
            {typeof(Guid?), "UNIQUEIDENTIFIER"},


        };

        public List<KeyValuePair<string, Type>> Fields { get; set; } = new List<KeyValuePair<string, Type>>();
        public string TableName { get; set; }

        public Type Type { get; set; }

        public void UpdateDataTypeDef(Dictionary<Type, string> dic)
        {
            if (dic == null) return;
            foreach (var item in dic)
            {
                if (!_dataMapper.ContainsKey(item.Key))
                {
                    _dataMapper.Add(item.Key, item.Value);
                }
            }
        }

        public void UpdateColumnDataTypeDef(Dictionary<string, string> dic)
        {
            if (dic == null) return;
            foreach (var item in dic)
            {
                if (!_columnDataMapper.ContainsKey(item.Key))
                {
                    _columnDataMapper.Add(item.Key, item.Value);
                }
            }
        }

        public Table(Type t, string locationId)
        {
            if (t == null)
            {
                throw new Exception("Type could not be null");
            }
            TableName = $"{t.Name}".Replace("Content", "");
            Type = t;
            LocationId = locationId;
            foreach (PropertyInfo p in t.GetProperties())
            {
                KeyValuePair<string, Type> field = new KeyValuePair<string, Type>(p.Name, p.PropertyType);

                Fields.Add(field);
            }
        }

        protected void ExecuteSql(string script)
        {
            
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Sql"].ConnectionString))
            {
                conn.Open();
                using (SqlCommand command = new SqlCommand(script, conn))
                {
                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (SqlException ex)
                    {
                        Console.WriteLine("Error :" + TableName + " - " + ex.Message);
                        
                    }
                }
            }
        }

        public void CleanExistingDataByLocationId(string locationId)
        {
            string script = string.Format("DELETE FROM {0} WHERE IntacctLocationId = '{1}'", TableName, locationId);

            ExecuteSql(script.ToString());
        }

        public void Create()
        {
            var dropTableScript = string.Format("IF OBJECT_ID('{0}', 'U') IS NOT NULL DROP TABLE {0}", TableName);

            StringBuilder script = new StringBuilder();
            script.AppendLine(dropTableScript);

            script.AppendLine("CREATE TABLE " + TableName);
            script.AppendLine("(");

            for (int i = 0; i < Fields.Count; i++)
            {
                KeyValuePair<string, Type> field = Fields[i];

                //seeking any definition data type by column name
                if (_columnDataMapper.ContainsKey(field.Key))
                {
                    script.Append("\t [" + field.Key + "] " + _columnDataMapper[field.Key]);
                }
                else if (_dataMapper.ContainsKey(field.Value))
                {
                    script.Append("\t [" + field.Key + "] " + _dataMapper[field.Value]);
                }
                else
                {
                    // Complex Type? 
                    script.Append("\t " + field.Key + " BIGINT");
                }

                if (i != Fields.Count - 1)
                {
                    script.Append(",");
                }

                script.Append(Environment.NewLine);
            }

            //Add organisationId columns

            script.Append("\t " + ", [IntacctLocationId]" + " nvarchar(50)");
            script.AppendLine(")");

            ExecuteSql(script.ToString());
        }

        public void Insert<T>(List<T> entities)
        {
            if (!entities.Any()) return;

            //split array if it has more than 1000 items
            if (entities.Count > 1000)
            {
                const int sizeChunk = 500;
                var data = Split(entities, sizeChunk);
                foreach (var list in data)
                {
                    Insert(list);
                }
            }
            else
            {
                var script = new StringBuilder();
                script.AppendLine("INSERT INTO " + TableName);
                script.AppendLine("(");

                int i = 0;
                foreach (var field in Fields)
                {
                    script.Append("[" + field.Key + "]");

                    if (i != Fields.Count - 1)
                    {
                        script.Append(",");
                    }
                    i++;
                }
                script.AppendLine(", [IntacctLocationId]) VALUES");

                string org = ConvertToString(LocationId);
                for (int j = 0; j < entities.Count; j++)
                {
                    var entity = entities[j];
                    script.Append("(");
                    for (int k = 0; k < Fields.Count; k++)
                    {
                        var value = GetPropValue(entity, Fields.ElementAt(k).Key);
                        script.Append(ConvertToString(value));
                        if (k != Fields.Count - 1)
                        {
                            script.Append(",");
                        }

                    }
                    //insert organisation value 
                    script.Append("," + org);
                    if (j != entities.Count - 1)
                    {
                        script.AppendLine("),");
                    }

                }
                script.AppendLine(")");
                string sql = script.ToString();
                ExecuteSql(sql);
            }


        }


        public static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName)?.GetValue(src, null);
        }


        private static string ConvertToString(object crmValue)
        {
            string stringValue = null;

            if (crmValue == null)
            {
                stringValue = "NULL";
            }
            else if (crmValue is Enum)
            {
                stringValue = ((int)crmValue).ToString();
            }
            else if (crmValue is double)
            {
                stringValue = ((double)crmValue).ToString(CultureInfo.CreateSpecificCulture("en-GB"));
            }
            else if (crmValue is decimal)
            {
                stringValue = ((decimal)crmValue).ToString(CultureInfo.CreateSpecificCulture("en-GB"));
            }
            else if (crmValue is bool)
            {
                stringValue = (((bool)crmValue) ? 1 : 0).ToString();
            }
            else if (crmValue is DateTime || crmValue is Guid)
            {
                stringValue = string.Format("'{0}'", crmValue);
            }
            else if (crmValue is string)
            {
                stringValue = string.Format("'{0}'", Sanitize(crmValue.ToString()));
            }
            else
            {
                stringValue = Sanitize(crmValue.ToString());
            }

            return stringValue;
        }

        private static string Sanitize(string str)
        {
            if (str.Length > 8000)
            {
                str = str.Substring(0, 8000);
            }

            return str.Replace("'", "\"");
        }


        public static List<List<T>> Split<T>(List<T> collection, int size)
        {
            var chunks = new List<List<T>>();
            var chunkCount = collection.Count() / size;

            if (collection.Count % size > 0)
                chunkCount++;

            for (var i = 0; i < chunkCount; i++)
                chunks.Add(collection.Skip(i * size).Take(size).ToList());

            return chunks;
        }

    }
}
