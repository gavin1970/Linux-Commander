using System;
using System.Collections.Generic;
using System.Data;

namespace JSONHelper
{
    public class JSONData : JSONWorker
    {
        #region Constructor
        /// <example>
        /// SQLData sqlData = new SQLData("connection string information");
        /// </example>
        /// <param name="connectionString"></param>
        public JSONData(string connectionString)
        {
            //check to ensure a valid connections tring was passed.
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                //create error
                LastError = new ArgumentException($"Connection string is required when initializing JSONData.");
                //pass the error back to the client
                throw LastError;
            }

            //set connection string and defaults
            ConnectionString = connectionString.ToString();

            //lets parse the connection string to see if we find a timeout within it.
            char[] seps = new char[] { ';' };
            string[] connArray = ConnectionString.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            //loop through what we found.
            foreach (string value in connArray)
            {
                string[] connValue = value.Trim().Split('=');

                if (connValue.Length < 2)
                    continue;

                switch (connValue[0].ToLower().Trim())
                {
                    case "file":
                        //get the value of timeout
                        JSONFilePath = connValue[1].Trim();
                        break;
                }

                if (!string.IsNullOrWhiteSpace(JSONFilePath))
                    break;
            }

            CheckFilePath();
        }
        #endregion

        /// <summary>
        /// Check if table exists.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public bool TableExists(string tableName)
        {
            //check to ensure a valid connections tring was passed.
            if (string.IsNullOrWhiteSpace(tableName))
                //create error
                ThrowException($"Connection string is required when initializing JSONData.");

            CheckFilePath();

            bool retVal = false;

            DataSet ds = RecordData;
            if (ds != null && ds.Tables.Contains(tableName))
                retVal = true;

            return retVal;
        }

        /// <summary>
        /// Create new table.  If table already exists, it will be overwritten, including structure and all data.
        /// </summary>
        /// <param name="newTable"></param>
        /// <returns></returns>
        public JSONStatus CreateTable(DataTable newTable)
        {
            CheckFilePath();

            DataSet ds = RecordData;
            if (ds.Tables.Contains(newTable.TableName))
                ThrowException("Table name already exists.");

            return UpdateTable(newTable);
        }

        /// <summary>
        /// create a table
        /// </summary>
        /// <param name="fieldValues"></param>
        /// <returns></returns>
        public JSONStatus CreateTable(string tableName, List<FIELD_VALUE> fieldValues)
        {
            JSONStatus retVal = new JSONStatus();
            DataTable dataTable = new DataTable();
            dataTable.TableName = tableName;

            try
            {
                //create table headers..
                foreach (var field in fieldValues)
                    dataTable.Columns.Add(field.FieldName);

                DataRow dr = dataTable.NewRow();

                foreach (var field in fieldValues)
                    dr[field.FieldName] = field.Value;

                dataTable.Rows.Add(dr);

                retVal = CreateTable(dataTable);
            } 
            catch(Exception ex)
            {
                retVal.Description = ex.Message;
                retVal.Status = RESULT_STATUS.EXCEPTION;
                retVal.StackTrace = ex.StackTrace;
            }

            return retVal;
        }

        /// <summary>
        /// Get all data and structure of a specific table.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public DataTable GetTable(string tableName, bool refresh = false)
        {
            if (refresh)
                LoadData(true);

            CheckFilePath();

            DataSet ds = RecordData;
            if (ds != null && ds.Tables.Count > 0)
            {
                if (ds.Tables.Contains(tableName))
                    return ds.Tables[tableName].Copy();
            }

            return null;
        }

        /// <summary>
        /// Query table by where clause and ordering the return as an array of rows.
        /// </summary>
        /// <example>
        /// DataRow[] drArray = jsonData.GetRecords(dataType, "Type LIKE '%American%'", "Type DESC");
        /// </example>
        /// <param name="tableName"></param>
        /// <param name="where"></param>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        public DataRow[] GetRecords(string tableName, string where, string orderBy = null)
        {
            CheckFilePath();

            if (where != null && where.StartsWith("WHERE "))
                where = where.Replace("WHERE ", "");

            if (orderBy != null && orderBy.StartsWith("ORDER BY "))
                orderBy = orderBy.Replace("ORDER BY ", "");

            DataTable dt = GetTable(tableName);

            if (dt == null)
                return null;
            else
                return dt.Select(where, orderBy);
        }

        /// <summary>
        /// Update Data Set
        /// </summary>
        /// <param name="newTable"></param>
        /// <returns></returns>
        public JSONStatus UpdateTable(DataTable newTable)
        {
            if (string.IsNullOrWhiteSpace(newTable.TableName))
                ThrowException("newTable.TableName is required to be set.");

            DataTable dataTable = newTable.Copy();
            DataSet ds = DropTable(newTable.TableName);

            ds.Tables.Add(dataTable);

            RecordData = ds;
            return SaveData();
        }

        /// <summary>
        /// Delte Data table
        /// </summary>
        /// <param name="newTable"></param>
        /// <returns></returns>
        public JSONStatus DropTable(DataTable newTable)
        {
            if (string.IsNullOrWhiteSpace(newTable.TableName))
                ThrowException("newTable.TableName is required to be set.");

            RecordData = DropTable(newTable.TableName);
            return SaveData();
        }

        /// <summary>
        /// Update specific record/records
        /// </summary>
        /// <example>
        /// List<JSONHelper.FIELD_VALUE> fieldValues = new List<JSONHelper.FIELD_VALUE>();
        /// fieldValues.Add(new JSONHelper.FIELD_VALUE { FieldName = "PMType", Value = inventoryModel.PMType });
        /// fieldValues.Add(new JSONHelper.FIELD_VALUE { FieldName = "ShapeType", Value = inventoryModel.ShapeType });
        /// JSONStatus rs = jsonData.UpdateRecord("Inventory", fieldValues, $"InventoryId='{inventoryModel.InventoryId}'");
        /// </example>
        /// <param name="tableName"></param>
        /// <param name="fieldValues"></param>
        /// <param name="where"></param>
        /// <returns></returns>
        public JSONStatus UpdateRecord(string tableName, List<FIELD_VALUE> fieldValues, string where, bool createTable = false)
        {
            CheckFilePath();

            JSONStatus retVal = new JSONStatus();
            DataTable dt = GetTable(tableName);

            try
            {
                if (dt != null)
                {
                    foreach (FIELD_VALUE fv in fieldValues)
                    {
                        if (!dt.Columns.Contains(fv.FieldName))
                            dt.Columns.Add(fv.FieldName);
                    }
                }
                else if (dt == null && createTable)
                {
                    CreateTable(tableName, fieldValues);
                    retVal.Description = "Success";
                    return retVal;
                }
                else if(dt == null)
                    ThrowException($"'{tableName}' table does not exists.  Use CreateTable() first.");

                DataRow[] exists = dt.Select(where);

                if (exists != null && exists.Length>0)
                {
                    for (int i = 0; i < exists.Length; i++)
                    {
                        dt.Rows.Remove(exists[i]);
                        DataRow dr = dt.NewRow();

                        foreach (FIELD_VALUE fv in fieldValues)
                            dr[fv.FieldName] = fv.Value;

                        dt.Rows.Add(dr);
                    }
                }
                else
                {
                    DataRow newRow = dt.NewRow();
                    foreach (FIELD_VALUE fv in fieldValues)
                        newRow[fv.FieldName] = fv.Value;
                    dt.Rows.Add(newRow);
                }

                RecordData.Tables.Remove(tableName);
                RecordData.Tables.Add(dt);
                SaveData();
            } 
            catch(Exception ex)
            {
                retVal.Status = RESULT_STATUS.EXCEPTION;
                retVal.Description = ex.Message;
                retVal.StackTrace = ex.StackTrace;
            }

            return retVal;
        }

        /// <summary>
        /// get DataSet and remove dataset, even if empty.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataSet DropTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                ThrowException("TableName is required to be set.");

            CheckFilePath();

            DataSet ds = RecordData;

            if (ds == null)
                ds = new DataSet();

            if (ds.Tables.Contains(tableName))
                ds.Tables.Remove(tableName);

            return ds;
        }
    }
}
