using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        public class SyntaxPattern
        {
            public string Pattern { get; set; }
            public Color Color { get; set; }

            public SyntaxPattern(string pattern, Color color)
            {
                Pattern = pattern;
                Color = color;
            }
        }

        private void ScheduleSyntaxHighlighting()
        {
            syntaxHighlightingTimer?.Stop();

            if (syntaxHighlightingTimer == null)
            {
                syntaxHighlightingTimer = new DispatcherTimer();
                syntaxHighlightingTimer.Tick += (s, e) =>
                {
                    syntaxHighlightingTimer.Stop();
                    ApplySyntaxHighlighting();
                };
            }

            syntaxHighlightingTimer.Start();
        }

        private void ApplySyntaxHighlightingImmediate()
        {
            syntaxHighlightingTimer?.Stop();

            lastHighlightedText = "";
            ApplySyntaxHighlighting();
        }

        private void ApplySyntaxHighlighting()
        {
            if (isApplyingSyntaxHighlighting) return;

            isApplyingSyntaxHighlighting = true;

            try
            {
                string text;
                CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

                if (text.Length > maxHighlightLength) return;

                if (text == lastHighlightedText) return;

                lastHighlightedText = text;

                var selection = CodeEditor.Document.Selection;
                int selectionStart = selection.StartPosition;
                int selectionEnd = selection.EndPosition;

                var range = CodeEditor.Document.GetRange(0, text.Length);
                range.CharacterFormat.ForegroundColor = DefaultBrush.Color;

                var patterns = GetSyntaxPatterns(currentLanguage);

                foreach (var pattern in patterns)
                {
                    var matches = Regex.Matches(text, pattern.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    foreach (Match match in matches)
                    {
                        var highlightRange = CodeEditor.Document.GetRange(match.Index, match.Index + match.Length);
                        highlightRange.CharacterFormat.ForegroundColor = pattern.Color;
                    }
                }

                CodeEditor.Document.Selection.SetRange(selectionStart, selectionEnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Syntax highlighting error: {ex.Message}");
            }
            finally
            {
                isApplyingSyntaxHighlighting = false;
            }
        }

        private List<SyntaxPattern> GetSyntaxPatterns(string language)
        {
            var patterns = new List<SyntaxPattern>();

            switch (language)
            {
                case "c":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"\b\d+\.?\d*[fFlL]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(int|char|float|double|void|struct|enum|typedef|const|static|extern|auto|register|volatile|sizeof|union|long|short|signed|unsigned)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"(?<=#\s*include\s*)[<""][^>""]+[>""]", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"\\[abfnrtv\\'\""]", EscapeSequenceBrush.Color),
                        new SyntaxPattern(@"^\s*#\s*\w+", PreprocessorBrush.Color),
                        new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color),
                    });
                    break;

                case "cpp":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"^\s*#\s*\w+", PreprocessorBrush.Color),
                        new SyntaxPattern(@"\b\d+\.?\d*[fFlL]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|true|false)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(int|char|float|double|void|bool|class|struct|enum|typedef|const|static|extern|auto|template|namespace|using|public|private|protected|virtual|override|new|delete|nullptr)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
                    });
                    break;

                case "csharp":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"\b\d+\.?\d*[fFdDmM]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|else|for|foreach|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|finally|true|false|null)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(int|char|float|double|decimal|string|bool|void|var|class|struct|enum|interface|namespace|using|public|private|protected|internal|static|abstract|virtual|override|new|this|base|typeof|sizeof)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"@""(?:[^""]|"""")*""", StringBrush.Color),
                        new SyntaxPattern(@"\\[abfnrtv\\'\""]", EscapeSequenceBrush.Color),
                        new SyntaxPattern(@"(?<=using\s+)[a-zA-Z_][\w\.]*(?=\s*;)", PreprocessorBrush.Color),
                        new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
                    });
                    break;
            }

            return patterns;
        }
    }
}
