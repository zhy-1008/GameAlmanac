using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameAlmanac
{
    public sealed class Metric
    {
        public readonly string Name;
        public readonly string[] Items;

        public Metric(string name, params string[] items)
        {
            Name = name;
            Items = items;
        }
    }

    public sealed class WeightTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int[] Weights { get; set; }
    }

    public sealed class GameType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TemplateId { get; set; }
    }

    public sealed class Game
    {
        public string Id;
        public string Name;
        public string Cover;
        public string TypeId;
        public string Note;
        public readonly List<Play> Plays = new List<Play>();

        public Play FindPlay(int year)
        {
            return Plays.FirstOrDefault(play => play.Year == year);
        }

        public Play PreviousPlay(int year)
        {
            return Plays.Where(play => play.Year < year).OrderByDescending(play => play.Year).FirstOrDefault();
        }

        public Play ScoreSource(Play play)
        {
            while (play != null && play.ReuseYear.HasValue)
                play = play.ReuseYear.Value < play.Year ? FindPlay(play.ReuseYear.Value) : null;
            return play;
        }
    }

    public sealed class Play
    {
        public int Year;
        public int? ReuseYear;
        public bool Detailed;
        // null is unscored. NaN marks a metric as not applicable.
        public readonly double?[] Direct = new double?[Rating.Metrics.Length];
        public readonly double?[][] Details = Rating.Metrics.Select(metric => new double?[metric.Items.Length]).ToArray();

        public Play(int year) { Year = year; }

        public Play IndependentCopy(int year)
        {
            var copy = new Play(year) { Detailed = Detailed };
            Direct.CopyTo(copy.Direct, 0);
            for (int index = 0; index < Details.Length; index++)
                Details[index].CopyTo(copy.Details[index], 0);
            return copy;
        }
    }

    public sealed class Library
    {
        public readonly List<WeightTemplate> Templates = new List<WeightTemplate>();
        public readonly List<GameType> Types = new List<GameType>();
        public readonly List<Game> Games = new List<Game>();

        public Game Game(string id) { return Games.FirstOrDefault(game => game.Id == id); }
        public GameType Type(string id) { return Types.First(type => type.Id == id); }
        public WeightTemplate Template(string id) { return Templates.First(template => template.Id == id); }
        public WeightTemplate TemplateFor(Game game) { return Template(Type(game.TypeId).TemplateId); }
    }

    public static class Rating
    {
        public static readonly Metric[] Metrics = {
            new Metric("玩法体验", "核心趣味性", "玩法深度", "自由度"),
            new Metric("内容体验", "内容丰富度", "内容设计", "耐玩性"),
            new Metric("视听表现", "美术风格", "画面表现", "音乐音效"),
            new Metric("叙事与沉浸", "剧情叙事", "角色与世界观", "沉浸感"),
            new Metric("交互与技术", "操作手感", "交互设计", "技术完成度"),
            new Metric("价值与诚意", "性价比", "玩家友好度", "独特性")
        };

        public static int[] DefaultWeights() { return new[] { 30, 20, 15, 15, 10, 10 }; }

        public static bool IsNotApplicable(double? value)
        {
            return value.HasValue && double.IsNaN(value.Value);
        }

        public static double? Group(double?[] scores)
        {
            var applicable = scores.Where(value => !IsNotApplicable(value)).ToArray();
            if (applicable.Length == 0) return double.NaN;
            if (applicable.Any(value => !value.HasValue)) return null;
            return applicable.Average(value => value.Value);
        }

        public static RatingResult Calculate(Play play, int[] weights)
        {
            var scores = play.Detailed ? play.Details.Select(Group).ToArray() : (double?[])play.Direct.Clone();
            var effective = new double[Metrics.Length];
            int included = Enumerable.Range(0, weights.Length).Where(index => !IsNotApplicable(scores[index])).Sum(index => weights[index]);
            if (included == 0) return new RatingResult(scores, effective, double.NaN);
            bool pending = false;
            double total = 0;
            for (int index = 0; index < weights.Length; index++)
            {
                if (IsNotApplicable(scores[index]) || weights[index] == 0) continue;
                effective[index] = weights[index] * 100.0 / included;
                if (!scores[index].HasValue) pending = true;
                else total += scores[index].Value * weights[index] / included;
            }
            return new RatingResult(scores, effective, pending ? (double?)null : total);
        }

        public static string Format(double? value)
        {
            if (!value.HasValue) return "未评分";
            if (IsNotApplicable(value)) return "不适用";
            return Math.Round(value.Value + 1e-10, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static bool TryScore(string text, out double? score)
        {
            score = null;
            text = text.Trim();
            if (text.Length == 0) return true;
            int point = text.IndexOf('.');
            double number;
            if (point >= 0 && text.Length - point > 2) return false;
            if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number)
                || double.IsNaN(number) || number < 0 || number > 10) return false;
            score = number;
            return true;
        }
    }

    public sealed class RatingResult
    {
        public readonly double?[] Scores;
        public readonly double[] EffectiveWeights;
        public readonly double? Total;

        public RatingResult(double?[] scores, double[] effectiveWeights, double? total)
        {
            Scores = scores;
            EffectiveWeights = effectiveWeights;
            Total = total;
        }
    }
}
