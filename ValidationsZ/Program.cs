using System;
using Dapper;
using Microsoft.Data.SqlClient;
using EntityClasses;
using System.Collections.Generic;
using System.Linq;
using GeneralUtilsNs;
using System.Text.RegularExpressions;
using Validations;
using Serilog;
using ConfigurationNs;
using System.IO;
using System.Text.Json;



namespace Validations
{

    public class Program
    {
        //do NOT call Validations directly because it is used by TWO solutions (pension and insurance)
        //it is compliled as a DLL 
        //Probably could it be compiled as exe and used in both apps


    }
}
