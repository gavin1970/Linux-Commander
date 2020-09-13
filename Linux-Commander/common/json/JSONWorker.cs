using Newtonsoft.Json;
using System;
using System.Data;
using System.IO;
using System.Text;

namespace JSONHelper
{
    public abstract class JSONWorker : JSONStatus
    {
        public struct FIELD_VALUE
        {
            public string FieldName;
            public string Value;
        }

        const string MISSING_FILE = "File path in connection string is required.  e.g. file=C:\\Path\\DataFile.data";

        #region Public Properties
        /// <summary>
        /// set errors that might occur
        /// </summary>
        public Exception LastError { get; set; } = null;
        #endregion

        #region Internal Properties
        /// <summary>
        /// Holds all data records as a data table.
        /// </summary>
        internal DataSet RecordData { get; set; } = null;

        /// <summary>
        /// set connection string
        /// </summary>
        internal string ConnectionString { get; set; } = null;

        /// <summary>
        /// Last Stored Procedure
        /// </summary>
        internal string LastStoredProcedure { get; set; } = null;

        /// <summary>
        /// Last Parameters Passed in.
        /// </summary>
        internal object[] LastParameters { get; set; } = null;

        /// <summary>
        /// Data File.
        /// </summary>
        internal string JSONFilePath { get; set; } = null;
        #endregion

        #region Internal Methods
        /// <summary>
        /// Internal user to validate params have been set along with data loaded.
        /// </summary>
        internal void CheckFilePath()
        {
            string errMsg = null;

            if (string.IsNullOrWhiteSpace(JSONFilePath))
                errMsg = MISSING_FILE;

            if (errMsg == null)
            {
                JSONStatus js = LoadData();
                if (js.Status != RESULT_STATUS.OK && js.Status != RESULT_STATUS.MISSING)
                    errMsg = js.Description;
            }

            //check to ensure a valid connections tring was passed.
            if (errMsg != null)
                //create error
                ThrowException(errMsg);
        }

        /// <summary>
        /// throw an exception and put it in the last error property
        /// </summary>
        /// <param name="msg"></param>
        internal void ThrowException(string msg)
        {
            //create error
            LastError = new Exception(msg);
            //pass the error back to the client
            throw LastError;
        }

        /// <summary>
        /// Load all data from JSON to DataSet "RecordData"
        /// </summary>
        /// <returns></returns>
        internal JSONStatus LoadData(bool refresh = false)
        {
            JSONStatus retVal = new JSONStatus();
            if (!refresh && RecordData != null && RecordData.Tables.Count != 0)
                return retVal;

            try
            {
                if (!File.Exists(JSONFilePath))
                {
                    retVal.Status = RESULT_STATUS.MISSING;
                    retVal.Description = $"Missing Data File: {JSONFilePath}";
                }
                else
                {
                    byte[] bytes = File.ReadAllBytes(JSONFilePath);
                    string someString = Encoding.UTF8.GetString(bytes);
                    RecordData = (DataSet)JsonConvert.DeserializeObject(someString, (typeof(DataSet)));
                    retVal.Description = "Success";
                }
            }
            catch (Exception ex)
            {
                retVal.Status = RESULT_STATUS.EXCEPTION;
                retVal.Description = ex.Message;
                retVal.StackTrace = ex.StackTrace;
            }

            return retVal;
        }

        /// <summary>
        /// Save all data from DataSet "RecordData" to JSON
        /// </summary>
        /// <returns></returns>
        internal JSONStatus SaveData()
        {
            JSONStatus retVal = new JSONStatus();
            try
            {
                if (RecordData == null)
                {
                    retVal.Status = RESULT_STATUS.MISSING;
                    retVal.Description = "Record data needs to be loaded and have structure before it can be saved.";
                    return retVal;
                }

                string json = JsonConvert.SerializeObject(RecordData, Formatting.Indented);
                byte[] bytes = Encoding.ASCII.GetBytes(json);
                
                if (File.Exists(JSONFilePath))
                    File.Delete(JSONFilePath);

                File.WriteAllBytes(JSONFilePath, bytes);
                retVal.Description = "Success";
            }
            catch(Exception ex)
            {
                retVal.Status = RESULT_STATUS.EXCEPTION;
                retVal.Description = ex.Message;
                retVal.StackTrace = ex.StackTrace;
            }

            return retVal;
        }
        #endregion
    }
}
