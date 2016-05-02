﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using CommandLine;
using System.Reflection;

namespace Microsoft.CodeAnalysis.Sarif.ConvertToSarif
{
    internal static class Program
    {
        /// <summary>The entry point for the SARIF file format conversion utility.</summary>
        /// <param name="args">Arguments passed in from the tool's command line.</param>
        /// <returns>0 on success; nonzero on failure.</returns>
        public static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<ConvertOptions, ListOptions>(args)
              .MapResult(
                (ConvertOptions convertOptions) => RunConvertFile(convertOptions),
                (ListOptions listOptions) => new ListCommand().Run(listOptions),
                errs => 1);
        }

        public static int RunConvertFile(ConvertOptions convertOptions)
        {
            try
            {
                ToolFormatConversionOptions toolFormatConversionOptions = 0;

                if (convertOptions.PrettyPrint)
                {
                    toolFormatConversionOptions |= ToolFormatConversionOptions.PrettyPrint;
                };

                if (convertOptions.Force)
                {
                    toolFormatConversionOptions |= ToolFormatConversionOptions.OverwriteExistingOutputFile;
                };

                if (string.IsNullOrEmpty(convertOptions.OutputFilePath))
                {
                    convertOptions.OutputFilePath = convertOptions.InputFilePath + ".sarif";
                }

                if (convertOptions.ToolFormat.Equals("PREfast", StringComparison.OrdinalIgnoreCase))
                {
                    string sarif = ToolFormatConverter.ConvertPREfastToStandardFormat(convertOptions.InputFilePath);
                    File.WriteAllText(convertOptions.OutputFilePath, sarif);
                }
                else
                {
                    string pluginFile;
                    if (String.IsNullOrWhiteSpace(convertOptions.ConverterFilePath))
                    {
                        pluginFile = GetDefaultPluginFile();
                    }
                    else
                    {
                        pluginFile = convertOptions.ConverterFilePath;
                    }

                    Assembly converterAssembly = Assembly.LoadFrom(pluginFile);
                    new ToolFormatConverter(converterAssembly).ConvertToStandardFormat(
                        convertOptions.ToolFormat,
                        convertOptions.InputFilePath,
                        convertOptions.OutputFilePath,
                        toolFormatConversionOptions);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }

            return 0;
        }

        private static string GetDefaultPluginFile()
        {
            return typeof(IToolFileConverter).Assembly.Location;
        }
    }
}
