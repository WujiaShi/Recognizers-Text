﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

using Microsoft.Recognizers.Definitions.English;
using Microsoft.Recognizers.Text.DateTime.Utilities;

namespace Microsoft.Recognizers.Text.DateTime.English
{
    public class EnglishTimeParserConfiguration : BaseOptionsConfiguration, ITimeParserConfiguration
    {
        public string TimeTokenPrefix { get; }

        public Regex AtRegex { get; }

        public Regex MealTimeRegex { get; }

        private static readonly Regex TimeSuffixFull =
            new Regex(DateTimeDefinitions.TimeSuffixFull, RegexOptions.Singleline);

        private static readonly Regex LunchRegex =
            new Regex(DateTimeDefinitions.LunchRegex, RegexOptions.Singleline);

        private static readonly Regex NightRegex =
            new Regex(DateTimeDefinitions.NightRegex, RegexOptions.Singleline);

        public IEnumerable<Regex> TimeRegexes { get; }

        public IImmutableDictionary<string, int> Numbers { get; }

        public IDateTimeUtilityConfiguration UtilityConfiguration { get; }

        public IDateTimeParser TimeZoneParser { get; }

        public EnglishTimeParserConfiguration(ICommonDateTimeParserConfiguration config) : base(config)
        {
            TimeTokenPrefix = DateTimeDefinitions.TimeTokenPrefix;
            AtRegex = EnglishTimeExtractorConfiguration.AtRegex;
            TimeRegexes = EnglishTimeExtractorConfiguration.TimeRegexList;
            UtilityConfiguration = config.UtilityConfiguration;
            Numbers = config.Numbers;
            TimeZoneParser = config.TimeZoneParser;
        }

        public void AdjustByPrefix(string prefix, ref int hour, ref int min, ref bool hasMin)
        {
            int deltaMin;

            var trimedPrefix = prefix.Trim().ToLowerInvariant();

            if (trimedPrefix.StartsWith("half"))
            {
                deltaMin = 30;
            }
            else if (trimedPrefix.StartsWith("a quarter") || trimedPrefix.StartsWith("quarter"))
            {
                deltaMin = 15;
            }
            else if (trimedPrefix.StartsWith("three quarter"))
            {
                deltaMin = 45;
            }
            else
            {
                var match = EnglishTimeExtractorConfiguration.LessThanOneHour.Match(trimedPrefix);
                var minStr = match.Groups["deltamin"].Value;
                if (!string.IsNullOrWhiteSpace(minStr))
                {
                    deltaMin = int.Parse(minStr);
                }
                else
                {
                    minStr = match.Groups["deltaminnum"].Value.ToLower();
                    deltaMin = Numbers[minStr];
                }
            }

            if (trimedPrefix.EndsWith("to"))
            {
                deltaMin = -deltaMin;
            }

            min += deltaMin;
            if (min < 0)
            {
                min += 60;
                hour -= 1;
            }

            hasMin = true;
        }

        public void AdjustBySuffix(string suffix, ref int hour, ref int min, ref bool hasMin, ref bool hasAm, ref bool hasPm)
        {
            var lowerSuffix = suffix.ToLowerInvariant();
            var deltaHour = 0;
            var match = TimeSuffixFull.MatchExact(lowerSuffix, trim: true);

            if (match.Success)
            {
                var oclockStr = match.Groups["oclock"].Value;
                if (string.IsNullOrEmpty(oclockStr))
                {
                    var amStr = match.Groups[Constants.AmGroupName].Value;
                    if (!string.IsNullOrEmpty(amStr))
                    {
                        if (hour >= Constants.HalfDayHourCount)
                        {
                            deltaHour = -Constants.HalfDayHourCount;
                        }
                        else
                        {
                            hasAm = true;
                        }
                    }

                    var pmStr = match.Groups[Constants.PmGroupName].Value;
                    if (!string.IsNullOrEmpty(pmStr))
                    {
                        if (hour < Constants.HalfDayHourCount)
                        {
                            deltaHour = Constants.HalfDayHourCount;
                        }

                        if (LunchRegex.IsMatch(pmStr))
                        {
                            // for hour>=10, <12
                            if (hour >=10 && hour <=Constants.HalfDayHourCount)
                            {
                                deltaHour = 0;
                                if (hour == Constants.HalfDayHourCount)
                                {
                                    hasPm = true;
                                }
                                else
                                {
                                    hasAm = true;
                                }
                            }
                            else
                            {
                                hasPm = true;
                            }
                        }
                        else if (NightRegex.IsMatch(pmStr))
                        {
                            //For hour <=3 or ==12, we treat it as am, for example 1 in the night (midnight) == 1am
                            if (hour <= 3 || hour == Constants.HalfDayHourCount)
                            {
                                if (hour == Constants.HalfDayHourCount)
                                {
                                    hour = 0;
                                }
                                deltaHour = 0;
                                hasAm = true;
                            }
                            else
                            {
                                hasPm = true;
                            }
                        }
                        else
                        {
                            hasPm = true;
                        }
                        
                    }
                }
            }

            hour = (hour + deltaHour) % 24;
        }
    }
}
