﻿// See https://aka.ms/new-console-template for more information

var xx1= @"C:\Users\kyrlo\soft\dotnet\insurance - project\TestingXbrl260\abc.xlsx";
   var fileName=  @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\curr2.xlsx";
var xx = CurrencyRates.CurrencyBatch.CurrencyBatchCreator(fileName, 2022, 0, 1);
var yy = 3;