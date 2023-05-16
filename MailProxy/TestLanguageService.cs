
namespace UidParser
{

    // https://github.com/microsoft/sqltoolsservice 
    class aaaaa
    {

        public static string ConnectionString
        {
            get{
                Microsoft.Data.SqlClient.SqlConnectionStringBuilder csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
                csb.Encrypt = false;
                csb.IntegratedSecurity = true;
                csb.DataSource = System.Environment.MachineName;
                csb.InitialCatalog = "COR_Basic_Demo_V4";
                return csb.ConnectionString;
            }
        }


        // 1. Install the Microsoft.SqlTools.ServiceLayer NuGet package in your C# project.
        // This package provides the APIs for interacting with sqltoolsservice.

        public static async System.Threading.Tasks.Task Test()
        {
            // https://github.com/microsoft/sqltoolsservice/blob/main/src/Microsoft.SqlTools.ServiceLayer/Microsoft.SqlTools.ServiceLayer.csproj
            // https://github.com/microsoft/sqltoolsservice/blob/036710e6340f52d03f5fbed9c2e7022550afd1f8/test/Microsoft.SqlTools.ServiceLayer.UnitTests/LanguageServer/CompletionServiceTest.cs#L23
            // https://github.com/microsoft/sqltoolsservice/blob/036710e6340f52d03f5fbed9c2e7022550afd1f8/src/Microsoft.SqlTools.ServiceLayer/LanguageServices/Completion/CompletionService.cs#L21
            // https://github.com/microsoft/sqltoolsservice/blob/036710e6340f52d03f5fbed9c2e7022550afd1f8/src/Microsoft.SqlTools.ServiceLayer/LanguageServices/LanguageService.cs#L1590
            // https://github.com/microsoft/sqltoolsservice/tree/main/test/Microsoft.SqlTools.Test.CompletionExtension


            // https://github.com/ststeiger/SqlParser/blob/master/SqlParser/Program.cs
            // Microsoft.SqlToolsService.

            /*
            var serviceHost = new ServiceHostBuilder()
                 .UseConnectionStrings(ConnectionString)
                 .Build();

            string scriptText = "SELECT * FROM MyTable WHERE MyColumn = 'value'";
            int cursorPosition = scriptText.Length; // end of script
            string databaseName = "MyDatabase";

            var service = serviceHost.GetService<CompletionService>();

            // The result object will contain a list of CompletionItem objects, 
            // which represent the completion suggestions for the SQL script.
            // Note that you may need to configure the sqltoolsservice 
            // to work with your particular database engine. 
            // You can do this by setting the appropriate options on the ServiceHostBuilder.
            var result = await service.ProvideCompletionsAsync(
                 new TextDocumentPositionParams
                 {
                     TextDocument = new TextDocumentIdentifier("file://temp.sql"),
                     Position = new Position
                     {
                         Line = 0,
                         Character = cursorPosition
                     }
                 },
                 databaseName,
                 scriptText
                );
            */

        }




    }
}
