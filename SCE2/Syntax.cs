using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TextControlBoxNS;
using Windows.UI;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        readonly string KeywordColor = "#569CD6";
        readonly string ControlFlowColor = "#D8A0DF";
        readonly string StringColor = "#CE9178";
        readonly string CommentColor = "#6A9955";
        readonly string NumberColor = "#B5CEA8";
        readonly string PreprocessorColor = "#9B9B9B";
        readonly string FunctionColor = "#DCDCAA";
        readonly string EscapeSequenceColor = "#FFCE54";

        readonly string KeywordColorDark = "#1F5F99";
        readonly string ControlFlowColorDark = "#8B4A9C";
        readonly string StringColorDark = "#A0522D";
        readonly string CommentColorDark = "#4A7C3B";
        readonly string NumberColorDark = "#6B8E5A";
        readonly string PreprocessorColorDark = "#6B6B6B";
        readonly string FunctionColorDark = "#B8860B";
        readonly string EscapeSequenceColorDark = "#E67E22";

        public void SelectLanguage(string extension)
        {
            switch (extension)
            {
                case "c":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "C Language Syntax Highlighting",
                        Name = "C",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("(", ")"),
                            new AutoPairingPair("/*", "*/")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"\b\d+\.?\d*[fFlL]?\b", NumberColorDark, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+\b", NumberColorDark, NumberColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionColorDark, FunctionColor),
                            new SyntaxHighlights(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return)\b", ControlFlowColorDark, ControlFlowColor),
                            new SyntaxHighlights(@"\b(int|char|float|double|void|struct|enum|asm|true|false|NULL|typedef|const|static|extern|auto|register|volatile|sizeof|union|long|short|signed|unsigned)\b", KeywordColorDark, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColorDark, StringColor),
                            new SyntaxHighlights(@"(?<=#\s*include\s*)[<""][^>""]+[>""]", StringColorDark, StringColor),
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColorDark, StringColor),
                            new SyntaxHighlights(@"\\[abfnrtv0\\'\""]", EscapeSequenceColorDark, EscapeSequenceColor),
                            new SyntaxHighlights(@"^\s*#\s*\w+", PreprocessorColorDark, PreprocessorColor),
                            new SyntaxHighlights(@"//.*?(?=\r|\n|$)", CommentColorDark, CommentColor),
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColorDark, CommentColor)
                        }

                    };
                    break;
                case "cpp":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "C++ Language Syntax Highlighting",
                        Name = "C++",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("(", ")"),
                            new AutoPairingPair("/*", "*/")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"^\s*#\s*\w+", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"\b\d+\.?\d*[fFlL]?\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|true|false)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"\b(int|char|float|double|void|bool|class|struct|enum|typedef|const|static|extern|auto|template|namespace|using|public|private|protected|virtual|override|new|delete|nullptr)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColor, StringColor),
                            new SyntaxHighlights(@"//.*?(?=\r|\n|$)", CommentColor, CommentColor),
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColor, CommentColor)
                        }
                    };
                    break;
                case "csharp":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "C# Language Syntax Highlighting",
                        Name = "C#",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("(", ")")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColor, StringColor),
                            new SyntaxHighlights(@"\b\d+\.?\d*[fFdDmM]?\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"\b(if|else|for|foreach|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|finally|true|false|null)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"\b(int|char|float|double|decimal|string|bool|void|var|class|struct|enum|interface|namespace|using|public|private|protected|internal|static|abstract|virtual|override|new|this|base|typeof|sizeof)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"@""(?:[^""]|"""")*""", StringColor, StringColor),
                            new SyntaxHighlights(@"\\[abfnrtv\\'\""]", EscapeSequenceColor, EscapeSequenceColor),
                            new SyntaxHighlights(@"(?<=using\s+)[a-zA-Z_][\w\.]*(?=\s*;)", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"//.*?(?=\r|\n|$)", CommentColor, CommentColor),
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColor, CommentColor)
                        }
                    };
                    break;
                case "java":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "Java Language Syntax Highlighting",
                        Name = "Java",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("(", ")")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"\b\d+\.?\d*[fFdDlL]?\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+[lL]?\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"\b(if|else|for|while|do|switch|case|default|break|continue|return|try|catch|throw|finally|true|false|null)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"\b(int|char|float|double|boolean|byte|short|long|void|class|interface|enum|extends|implements|package|import|public|private|protected|static|final|abstract|synchronized|volatile|transient|native|strictfp|this|super|new|instanceof)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColor, StringColor),
                            new SyntaxHighlights(@"@\w+", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"//.*?(?=\r|\n|$)", CommentColor, CommentColor),
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColor, CommentColor)
                        }
                    };
                    currentLanguage = "java";
                    break;
                case "js":
                case "javascript":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "JavaScript Language Syntax Highlighting",
                        Name = "JavaScript",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("`"),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("(", ")")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"\b\d+\.?\d*\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_$]\w*)\s*(?=\()", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"\b(if|else|for|while|do|switch|case|default|break|continue|return|try|catch|throw|finally|true|false|null|undefined|async|await|yield)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"\b(var|let|const|function|class|extends|import|export|from|as|new|this|super|typeof|instanceof|in|of|delete)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColor, StringColor),
                            new SyntaxHighlights(@"`(?:[^`\\]|\\.)*`", StringColor, StringColor),
                            new SyntaxHighlights(@"//.*?(?=\r|\n|$)", CommentColor, CommentColor),
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColor, CommentColor)
                        }
                    };
                    break;
                case "py":
                case "python":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "Python Language Syntax Highlighting",
                        Name = "Python",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("'''", "'''"),
                            new AutoPairingPair("\"\"\"", "\"\"\""),
                            new AutoPairingPair("(", ")"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("{", "}")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"\b\d+\.?\d*\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"\b(if|elif|else|for|while|break|continue|return|try|except|finally|raise|with|as|pass|lambda|yield|True|False|None|and|or|not|is|in)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"\b(def|class|import|from|global|nonlocal|async|await)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColor, StringColor),
                            new SyntaxHighlights(@"'''[\s\S]*?'''", StringColor, StringColor),
                            new SyntaxHighlights(@"f[""'](?:[^""'\\]|\\.)*[""']", StringColor, StringColor),
                            new SyntaxHighlights(@"@\w+", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"#.*?(?=\r|\n|$)", CommentColor, CommentColor)
                        }
                    };
                    break;
                case "rs":
                case "rust":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "Rust Language Syntax Highlighting",
                        Name = "Rust",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("(", ")")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"\b\d+\.?\d*[fFuUiI]*\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_]\w*)!\s*(?=\()", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"\b(if|else|match|for|while|loop|break|continue|return|true|false)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"\b(fn|let|mut|const|static|struct|enum|impl|trait|type|mod|use|pub|crate|super|self|Self|where|unsafe|extern|async|await|move)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"\b(i8|i16|i32|i64|i128|isize|u8|u16|u32|u64|u128|usize|f32|f64|bool|char|str)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColor, StringColor),
                            new SyntaxHighlights(@"r#""(?:[^""])*""#", StringColor, StringColor),
                            new SyntaxHighlights(@"#\[.*?\]", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"//.*?(?=\r|\n|$)", CommentColor, CommentColor),
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColor, CommentColor)
                        }
                    };
                    break;
                case "go":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "Go Language Syntax Highlighting",
                        Name = "Go",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\'"),
                            new AutoPairingPair("\""),
                            new AutoPairingPair("`"),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]"),
                            new AutoPairingPair("(", ")")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"\b\d+\.?\d*\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b0[xX][0-9a-fA-F]+\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"\b(if|else|for|switch|case|default|break|continue|goto|return|defer|go|select|fallthrough|true|false|nil)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"\b(var|const|func|package|import|type|struct|interface|map|chan|range)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"\b(int|int8|int16|int32|int64|uint|uint8|uint16|uint32|uint64|uintptr|float32|float64|complex64|complex128|bool|byte|rune|string|error)\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""(?:[^""\\]|\\.)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'(?:[^'\\]|\\.)*'", StringColor, StringColor),
                            new SyntaxHighlights(@"`[^`]*`", StringColor, StringColor),
                            new SyntaxHighlights(@"//.*?(?=\r|\n|$)", CommentColor, CommentColor),
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColor, CommentColor)
                        }
                    };
                    break;
                case "html":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "HTML Syntax Highlighting",
                        Name = "HTML",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\""),
                            new AutoPairingPair("'"),
                            new AutoPairingPair("<", ">")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"<!--[\s\S]*?-->", CommentColor, CommentColor),
                            new SyntaxHighlights(@"</?[a-zA-Z][a-zA-Z0-9]*\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"\b[a-zA-Z-]+(?=\s*=)", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"""[^""]*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'[^']*'", StringColor, StringColor),
                            new SyntaxHighlights(@"&[a-zA-Z][a-zA-Z0-9]*;", EscapeSequenceColor, EscapeSequenceColor),
                            new SyntaxHighlights(@"<!DOCTYPE[^>]*>", PreprocessorColor, PreprocessorColor)
                        }
                    };
                    break;
                case "css":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "CSS Syntax Highlighting",
                        Name = "CSS",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\""),
                            new AutoPairingPair("'"),
                            new AutoPairingPair("{", "}")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"/\*[\s\S]*?\*/", CommentColor, CommentColor),
                            new SyntaxHighlights(@"[a-zA-Z-]+\s*(?=:)", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"#[a-zA-Z][a-zA-Z0-9-]*", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"\.[a-zA-Z][a-zA-Z0-9-]*", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"""[^""]*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'[^']*'", StringColor, StringColor),
                            new SyntaxHighlights(@"\b\d+\.?\d*(px|em|rem|%|vh|vw|pt|pc|in|cm|mm|ex|ch|vmin|vmax|fr)?\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"#[0-9a-fA-F]{3,8}\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b(rgb|rgba|hsl|hsla|url)\s*\(", FunctionColor, FunctionColor),
                            new SyntaxHighlights(@"@[a-zA-Z-]+", ControlFlowColor, ControlFlowColor)
                        }
                    };
                    break;
                case "xml":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "XML Syntax Highlighting",
                        Name = "XML",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\""),
                            new AutoPairingPair("'"),
                            new AutoPairingPair("<", ">")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"<!--[\s\S]*?-->", CommentColor, CommentColor),
                            new SyntaxHighlights(@"<\?xml[^>]*\?>", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"</?[a-zA-Z][a-zA-Z0-9:.-]*\b", KeywordColor, KeywordColor),
                            new SyntaxHighlights(@"\b[a-zA-Z][a-zA-Z0-9:.-]*(?=\s*=)", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"""[^""]*""", StringColor, StringColor),
                            new SyntaxHighlights(@"'[^']*'", StringColor, StringColor),
                            new SyntaxHighlights(@"&[a-zA-Z][a-zA-Z0-9]*;", EscapeSequenceColor, EscapeSequenceColor),
                            new SyntaxHighlights(@"<!\[CDATA\[[\s\S]*?\]\]>", StringColor, StringColor)
                        }
                    };
                    break;
                case "json":
                    CodeEditor.SyntaxHighlighting = new SyntaxHighlightLanguage
                    {
                        Author = "Valerio Tangari",
                        Description = "JSON Syntax Highlighting",
                        Name = "JSON",
                        AutoPairingPair = new AutoPairingPair[]
                        {
                            new AutoPairingPair("\""),
                            new AutoPairingPair("{", "}"),
                            new AutoPairingPair("[", "]")
                        },
                        Highlights = new SyntaxHighlights[]
                        {
                            new SyntaxHighlights(@"""[^""\\]*(?:\\.[^""\\]*)*""(?=\s*:)", PreprocessorColor, PreprocessorColor),
                            new SyntaxHighlights(@"""[^""\\]*(?:\\.[^""\\]*)*""", StringColor, StringColor),
                            new SyntaxHighlights(@"\b\d+\.?\d*\b", NumberColor, NumberColor),
                            new SyntaxHighlights(@"\b(true|false|null)\b", ControlFlowColor, ControlFlowColor),
                            new SyntaxHighlights(@"[{}[\],:]", KeywordColor, KeywordColor)
                        }
                    };
                    break;
                default:
                    CodeEditor.SyntaxHighlighting = null;
                    break;
            }
        }
    }
}