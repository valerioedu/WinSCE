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

                case "java":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"\b\d+\.?\d*[fFdDlL]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+[lL]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|return|try|catch|throw|finally|true|false|null)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(int|char|float|double|boolean|byte|short|long|void|class|interface|enum|extends|implements|package|import|public|private|protected|static|final|abstract|synchronized|volatile|transient|native|strictfp|this|super|new|instanceof)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"@\w+", PreprocessorBrush.Color),
                        new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
                    });
                    break;

                case "javascript":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"\b\d+\.?\d*\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_$]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|return|try|catch|throw|finally|true|false|null|undefined|async|await|yield)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(var|let|const|function|class|extends|import|export|from|as|new|this|super|typeof|instanceof|in|of|delete)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"`(?:[^`\\]|\\.)*`", StringBrush.Color),
                        new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
                    });
                    break;

                case "python":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"\b\d+\.?\d*\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|elif|else|for|while|break|continue|return|try|except|finally|raise|with|as|pass|lambda|yield|True|False|None|and|or|not|is|in)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(def|class|import|from|global|nonlocal|async|await)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"'''[\s\S]*?'''", StringBrush.Color),
                        new SyntaxPattern(@"f[""'](?:[^""'\\]|\\.)*[""']", StringBrush.Color),
                        new SyntaxPattern(@"@\w+", PreprocessorBrush.Color),
                        new SyntaxPattern(@"#.*?(?=\r|\n|$)", CommentBrush.Color)
                    });
                    break;

                case "rust":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"\b\d+\.?\d*[fFuUiI]*\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)!\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|else|match|for|while|loop|break|continue|return|true|false)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(fn|let|mut|const|static|struct|enum|impl|trait|type|mod|use|pub|crate|super|self|Self|where|unsafe|extern|async|await|move)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"\b(i8|i16|i32|i64|i128|isize|u8|u16|u32|u64|u128|usize|f32|f64|bool|char|str)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"r#""(?:[^""])*""#", StringBrush.Color),
                        new SyntaxPattern(@"#\[.*?\]", PreprocessorBrush.Color),
                        new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
                    });
                    break;

                case "go":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"\b\d+\.?\d*\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|else|for|switch|case|default|break|continue|goto|return|defer|go|select|fallthrough|true|false|nil)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(var|const|func|package|import|type|struct|interface|map|chan|range)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"\b(int|int8|int16|int32|int64|uint|uint8|uint16|uint32|uint64|uintptr|float32|float64|complex64|complex128|bool|byte|rune|string|error)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"`[^`]*`", StringBrush.Color),
                        new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
                    });
                    break;

                case "html":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"<!--[\s\S]*?-->", CommentBrush.Color),
                        new SyntaxPattern(@"</?[a-zA-Z][a-zA-Z0-9]*\b", KeywordBrush.Color),
                        new SyntaxPattern(@"\b[a-zA-Z-]+(?=\s*=)", PreprocessorBrush.Color),
                        new SyntaxPattern(@"""[^""]*""", StringBrush.Color),
                        new SyntaxPattern(@"'[^']*'", StringBrush.Color),
                        new SyntaxPattern(@"&[a-zA-Z][a-zA-Z0-9]*;", EscapeSequenceBrush.Color),
                        new SyntaxPattern(@"<!DOCTYPE[^>]*>", PreprocessorBrush.Color)
                    });
                    break;

                case "css":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color),
                        new SyntaxPattern(@"[a-zA-Z-]+\s*(?=:)", PreprocessorBrush.Color),
                        new SyntaxPattern(@"#[a-zA-Z][a-zA-Z0-9-]*", KeywordBrush.Color),
                        new SyntaxPattern(@"\.[a-zA-Z][a-zA-Z0-9-]*", KeywordBrush.Color),
                        new SyntaxPattern(@"""[^""]*""", StringBrush.Color),
                        new SyntaxPattern(@"'[^']*'", StringBrush.Color),
                        new SyntaxPattern(@"\b\d+\.?\d*(px|em|rem|%|vh|vw|pt|pc|in|cm|mm|ex|ch|vmin|vmax|fr)?\b", NumberBrush.Color),
                        new SyntaxPattern(@"#[0-9a-fA-F]{3,8}\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b(rgb|rgba|hsl|hsla|url)\s*\(", FunctionBrush.Color),
                        new SyntaxPattern(@"@[a-zA-Z-]+", ControlFlowBrush.Color)
                    });
                    break;

                case "xml":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"<!--[\s\S]*?-->", CommentBrush.Color),
                        new SyntaxPattern(@"<\?xml[^>]*\?>", PreprocessorBrush.Color),
                        new SyntaxPattern(@"</?[a-zA-Z][a-zA-Z0-9:.-]*\b", KeywordBrush.Color),
                        new SyntaxPattern(@"\b[a-zA-Z][a-zA-Z0-9:.-]*(?=\s*=)", PreprocessorBrush.Color),
                        new SyntaxPattern(@"""[^""]*""", StringBrush.Color),
                        new SyntaxPattern(@"'[^']*'", StringBrush.Color),
                        new SyntaxPattern(@"&[a-zA-Z][a-zA-Z0-9]*;", EscapeSequenceBrush.Color),
                        new SyntaxPattern(@"<!\[CDATA\[[\s\S]*?\]\]>", StringBrush.Color)
                    });
                    break;

                case "json":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"""[^""\\]*(?:\\.[^""\\]*)*""(?=\s*:)", PreprocessorBrush.Color),
                        new SyntaxPattern(@"""[^""\\]*(?:\\.[^""\\]*)*""", StringBrush.Color),
                        new SyntaxPattern(@"\b\d+\.?\d*\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b(true|false|null)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"[{}[\],:]", KeywordBrush.Color)
                    });
                    break;
                case "markdown":
                    patterns.AddRange(new[]
                    {
                        new SyntaxPattern(@"^#{1,6}\s+.*?(?=\r|\n|$)", KeywordBrush.Color),
                        // TODO: Add more patterns for markdown
                    });
                    break;
            }

            return patterns;
        }
    }
}