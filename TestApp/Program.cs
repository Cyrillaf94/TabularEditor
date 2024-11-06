using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TabularEditor.UIServices;
using BPA = TabularEditor.BestPracticeAnalyzer;
using TabularEditor.TOMWrapper;

namespace TabularModelAnalyzer
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide the model file path as an argument.");
                return 1;
            }

            string modelPath = args[0];
            string externalRulesUrl = "http://raw.githubusercontent.com/microsoft/Analysis-Services/master/BestPracticeRules/BPARules.json";

            using (var writer = new StringWriter()) // This writer discards output
            {
                var originalOut = Console.Out;
                Console.SetOut(writer); // Redirect Console output to StringWriter

                JObject scoreReport;
                try
                {
                    scoreReport = AnalyzeModel(modelPath, externalRulesUrl);
                }
                catch (Exception ex)
                {
                    // Restore original Console output and write the error
                    Console.SetOut(originalOut);
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return 1;
                }

                // Restore original Console output for displaying results
                Console.SetOut(originalOut);
                Console.WriteLine(scoreReport.ToString());
                return 0;
            }
        }

        static JObject AnalyzeModel(string modelPath, string rulesUrl)
        {
            var settings = new TabularModelHandlerSettings
            {
                AutoFixup = true,
                ChangeDetectionLocalServers = false,
                PBIFeaturesOnly = false
            };

            var handler = new TabularModelHandler(modelPath, settings);
            var model = handler.Model;

            var bpa = new BPA.Analyzer();
            bpa.SetModel(model);

            var rules = LoadRules(rulesUrl);
            if (rules == null || rules.Count == 0)
            {
                throw new InvalidOperationException("No rules loaded from the external URL. Please verify the URL and rule format.");
            }

            bpa.ExternalRuleCollections.Add(rules);
            var results = bpa.AnalyzeAll().ToList();

            return GenerateScoreReport(model, results);
        }

        static BPA.BestPracticeCollection LoadRules(string url)
        {
            return BPA.BestPracticeCollection.GetCollectionFromUrl(url);
        }

        static JObject GenerateScoreReport(Model model, System.Collections.Generic.List<BPA.AnalyzerResult> results)
        {
            int infos = 0;
            int warnings = 0;
            int errors = 0;

            foreach (var result in results)
            {
                switch (result.Rule.Severity)
                {
                    case 1:
                        infos++;
                        break;
                    case 2:
                        warnings++;
                        break;
                    case 3:
                        errors++;
                        break;
                }
            }

            int measures = model.AllMeasures.Count();
            int columns = model.AllColumns.Count();
            int objects = measures + columns;

            int totalPenalty = (warnings + (errors * 5)) * 5;
            double unboundScore = 10 - (totalPenalty / (double)objects);
            double finalScore = Math.Max(unboundScore, 0);

            return new JObject
            {
                { "objects", objects },
                { "errors", errors },
                { "warnings", warnings },
                { "infos", infos },
                { "score", finalScore.ToString("F2", CultureInfo.InvariantCulture) }
            };
        }
    }
}
