namespace Spyglass.Utilities
 {
     public class QueryResult
     {
         public bool Successful { get; }
         
         public string Message { get;}
 
         public QueryResult(bool success, string message)
         {
             Successful = success;
             Message = message;
         }
         
         public static QueryResult FromSuccess(string message = "No message specified.")
         {
             return new QueryResult(true, message);
         }
 
         public static QueryResult FromError(string message = "No message specified.")
         {
             return new QueryResult(false, message);
         }
     }
     
     public class QueryResult<T>
     {
         public bool Successful { get; }
         
         public string Message { get; }
        
         public T Result { get;}
 
         public QueryResult(bool success, string message, T result)
         {
             Successful = success;
             Message = message;
             Result = result;
         }
        
         public static QueryResult<T> FromSuccess(string message = "No message specified.", T result = default)
         {
             return new QueryResult<T>(true, message, result);
         }

         public static QueryResult<T> FromError(string message = "No message specified.", T result = default)
         {
             return new QueryResult<T>(false, message, result);
         }
     }
 }