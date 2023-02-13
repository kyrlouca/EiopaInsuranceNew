using System;
using GeneralUtilsNs;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Validations;
using EiopaConstants;
using ConfigurationNs;
using Microsoft.Identity.Client;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Reflection.Metadata;
using EntityClassesZ;
using XbrlReader;
using System.Reflection.Metadata.Ecma335;

namespace AdhocTesting
{
    internal enum ValidStatus { A, Q1, Q2, Q3, Q4 };
    public class Program
    {
        public static readonly string SolvencyVersion = "IU260";

        enum Fts { exp, count, empty, isfallback, min, max, sum, matches, ftdv, ExDimVal };
        static void Main(string[] args)
        {
 


        }
    


    }
}
