﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QUT.Gppg;

using PascalABCCompiler.SyntaxTree;
using PascalABCCompiler.Errors;

namespace UniversalParserHelper
{
    public static class StringResources
    {
        private static string prefix = "PASCALABCPARSER_";
        public static string Get(string Id)
        {
            string ret = PascalABCCompiler.StringResources.Get(prefix + Id);
            if (ret == prefix + Id)
                return Id;
            else
                return ret;
        }
    }
    public abstract class UniversalParserHelper
    {
        private const int max_char_const = 0xFFFF;
        // SSM: Errors инициализируется в другом месте - сюда только передается!
        public List<Error> errors;
        public List<CompilerWarning> warnings;
        public System.Collections.Stack NodesStack; // SSM: для каких-то вспомогательных целей в двух правилах
        public bool build_tree_for_formatter = false;
        public bool build_tree_for_format_strings = false;
        public string CurrentFileName;
        public List<compiler_directive> compilerDirectives;

        public List<var_def_statement> pascalABC_var_statements;
        public List<type_declaration> pascalABC_type_declarations;
        public UniversalParserHelper()
        {
            NodesStack = new System.Collections.Stack();
            pascalABC_var_statements = new List<var_def_statement>();
            pascalABC_type_declarations = new List<type_declaration>();
        }
        public string directive_parameter(string s)
        {
            var ind = s.IndexOf(" ");
            if (ind < 0)
                return "";
            else
            {
                s = s.Substring(ind + 1);
                s = s.TrimEnd('}');
                return s;
            }
        }
        public ident create_directive_name(string text, SourceContext sc)
        {
            ident dn = new ident(new string(text.ToCharArray(1, text.Length - 1)));
            dn.source_context = sc;
            return dn;
        }

        public string RemoveThousandsDelimiter(string s, SourceContext sc)
        {
            if (s.EndsWith("_") || s.Contains("__"))
            {
                var errstr = ParserErrorsStringResources.Get("BAD_FORMED_NUM_CONST");
                errors.Add(new SyntaxError(errstr, CurrentFileName, sc, null));
            }

            return s.Replace("_", "");
        }
        private const_node create_int_const(string text, SourceContext sc, System.Globalization.NumberStyles NumberStyles)
        {
            //таблица целых констант на уровне синтаксиса
            //      не может быть - 0 +
            // 32--------16----8----|----8----16--------32----------------64(bits)
            // [  int64  )[       int32       ](  int64 ](      uint64     ]
            text = RemoveThousandsDelimiter(text, sc);
            if (NumberStyles == System.Globalization.NumberStyles.HexNumber)
                text = text.Substring(1);
            const_node cn = new int32_const();
            if (text.Length < 8)
                (cn as int32_const).val = Int32.Parse(text, NumberStyles);
            else
            {
                try
                {
                    UInt64 uint64 = UInt64.Parse(text, NumberStyles);
                    if (uint64 <= Int32.MaxValue)
                        (cn as int32_const).val = (Int32)uint64;
                    else
                        if (uint64 <= Int64.MaxValue)
                        cn = new int64_const((Int64)uint64);
                    else
                        cn = new uint64_const(uint64);
                }
                catch (Exception)
                {
                    if (NumberStyles == System.Globalization.NumberStyles.HexNumber)
                        errors.Add(new BadHex(CurrentFileName, sc, null));
                    else
                        errors.Add(new BadInt(CurrentFileName, sc, null));
                }
            }
            cn.source_context = sc;
            return cn;
        }
        public const_node create_int_const(string text, SourceContext sc)
        {
            return create_int_const(text, sc, System.Globalization.NumberStyles.Integer);
        }
        public const_node create_double_const(string text, SourceContext sc)
        {
            const_node cn = null;
            try
            {
                System.Globalization.NumberFormatInfo sgnfi = new System.Globalization.NumberFormatInfo();
                sgnfi.NumberDecimalSeparator = ".";

                text = RemoveThousandsDelimiter(text, sc);

                double val = double.Parse(text, sgnfi);
                cn = new double_const(val);
                cn.source_context = sc;
            }
            catch (Exception)
            {
                errors.Add(new BadFloat(CurrentFileName, sc, null));
            }
            return cn;
        }
        public const_node create_bigint_const(string text, SourceContext sc)
        {
            text = RemoveThousandsDelimiter(text, sc);

            var txt = text.Substring(0, text.Length - 2);
            var cn = new bigint_const();
            try
            {
                cn.val = System.UInt64.Parse(txt);
            }
            catch (Exception)
            {
                errors.Add(new BadInt(CurrentFileName, sc, null));
            }
            cn.source_context = sc;
            return cn;
        }
        public ident create_ident(string text, SourceContext sc)
        {
            if (text[0] == '&')
                text = text.Substring(1);
            ident id = new ident(text);
            id.source_context = sc;
            return id;
        }

        public string ReplaceSpecialSymbols(string text)
        {
            text = text.Replace("''", "'");
            return text;
        }

        public char_const create_char_const(string text, SourceContext sc)
        {
            string char_text = new string(text.ToCharArray(1, text.Length - 2));
            char_text = ReplaceSpecialSymbols(char_text);
            char_const ct = new char_const();
            ct.source_context = sc;
            if (char_text.Length == 1)
            {
                ct.cconst = char_text[0];
                return ct;
            }
            return null;
        }

        public sharp_char_const create_sharp_char_const(string text, SourceContext sc)
        {
            string int_text = new string(text.ToCharArray(1, text.Length - 1));
            sharp_char_const scc = null;
            int val = 0;
            if (int.TryParse(int_text, out val))
            {
                if (val > max_char_const)
                {
                    scc = new sharp_char_const(0);
                    errors.Add(new TooBigCharNumberInSharpCharConstant(CurrentFileName, sc, scc));
                }
                else
                    scc = new sharp_char_const(val);
                scc.source_context = sc;
            }
            else
            {
                errors.Add(new TooBigCharNumberInSharpCharConstant(CurrentFileName, sc, scc));
            }
            return scc;
        }

        public literal create_string_const(string text, SourceContext sc)
        {
            literal lt;
            if (text.Length == 3 && text[0] == '\'' && text[2] == '\'')
            {
                lt = new char_const(text[1]);
                lt.source_context = sc;
                return lt;
            }
            text = ReplaceSpecialSymbols(text.Substring(1, text.Length - 2));
            lt = new string_const(text);
            lt.source_context = sc;
            return lt;
        }

        public literal create_format_string_const(string text, SourceContext sc)
        {
            literal lt;
            text = ReplaceSpecialSymbols(text.Substring(2, text.Length - 3));
            lt = new string_const(text);
            (lt as string_const).IsInterpolated = true;
            lt.source_context = sc;
            return lt;
        }

        public void AddError(string message, LexLocation loc)
        {
            errors.Add(new SyntaxError(message, CurrentFileName, loc, null));
        }
        public string CreateErrorString(string yytext, LexLocation yyloc, params object[] args)
        {
            string prefix = "";
            if (yytext != "")
                prefix = StringResources.Get("FOUND{0}");
            else
                prefix = StringResources.Get("FOUNDEOF");

            if (this.build_tree_for_format_strings && prefix == StringResources.Get("FOUNDEOF"))
            {
                yytext = "}";
                prefix = StringResources.Get("FOUND{0}");
            }

            // Преобразовали в список строк - хорошо
            string expected = String.Join(", ", args.Skip(1).Select(x => x.ToString()));

            var ExpectedString = StringResources.Get("EXPECTED{1}");

            // string w = string.Join(" или ", tokens.Select(s => ConvertToHumanName((string)s)));

            return string.Format(prefix + ExpectedString, "'" + yytext + "'", expected);
        }
       /* public string CreateErrorString(string yytext, LexLocation yyloc, params object[] args)
        {
            string expected = String.Join(", ", args.Skip(1).Select(x => x.ToString()));
            string err = $"PARSER ERROR \"{yytext}\" AT LINE #{yyloc.StartLine}: EXPECTED  {expected}, FOUND {args[0]}";
            return err;
        }*/
        public List<object> ident_list11(object lr1, object lr3)
        {
            List<object> ar = (List<object>)lr3;
            ar.Insert(0, lr1);
            return ar;
        }

        public List<object> ident_list12(object lr0)
        {
            List<object> ar = new List<object>();
            ar.Add(lr0);
            return ar;
        }

        public List<object> ident_list21(object lr0, object lr2)
        {
            List<object> ar = (List<object>)lr0;
            ar.Add(lr2);
            return ar;
        }

        public List<object> ident_list13(object lr1, object lr3, object lr5)
        {
            List<object> ar = (List<object>)lr5;
            //named_type_reference n_t_r = (named_type_reference)lr3;
            var_def_statement vds = new var_def_statement();
            vds.vars = new ident_list();
            vds.vars.idents.Add((ident)lr1);
            vds.vars_type = (type_definition)lr3;//n_t_r;
            ar.Insert(0, vds);
            return ar;
        }

        public List<object> ident_list14(object lr1, object lr3)
        {
            List<object> ar = new List<object>();
            //named_type_reference n_t_r = (named_type_reference)lr3;
            var_def_statement vds = new var_def_statement();
            vds.vars = new ident_list();
            vds.vars.idents.Add((ident)lr1);
            vds.vars_type = (type_definition)lr3;
            ar.Add(vds);
            return ar;
        }
        public void create_source_context(object to, object left, object right)
        {
            if (to != null)
                ((syntax_tree_node)to).source_context = get_source_context(left, right);
        }

        public SourceContext get_source_context(object left, object right)
        {
            //debug
            /*if (left == null && right!=null)
            {
                Console.WriteLine("\n\rerror: left is null(create_source_context)!\n\r");
                Console.WriteLine(((syntax_tree_node)right).source_context.ToString());
            }
            if (right == null && left!=null)
            {
                Console.WriteLine("\n\rerror: right is null(create_source_context)!\n\r");
                Console.WriteLine(((syntax_tree_node)left).source_context.ToString());
            }
            if (((syntax_tree_node)left).source_context == null)
            {
                Console.WriteLine("\n\rerror: source_context is null!(left)\n\r");
                return null;
            }
            if (((syntax_tree_node)right).source_context == null)
            {
                Console.WriteLine("\n\rerror: source_context is null!(right)\n\r");
                return null;
            }
            */
            if ((left == null) || (right == null) || (((syntax_tree_node)left).source_context == null) || (((syntax_tree_node)right).source_context == null))
                return null;
            return new SourceContext(((syntax_tree_node)left).source_context, ((syntax_tree_node)right).source_context);
        }

        public void create_source_context_left(object to, object left)
        {
            file_position fp = ((syntax_tree_node)left).source_context.begin_position;
            ((syntax_tree_node)to).source_context = new SourceContext(fp.line_num, fp.column_num, fp.line_num, fp.column_num, 0, 0);
        }

        public void create_source_context_right(object to, object right)
        {
            file_position fp = ((syntax_tree_node)right).source_context.end_position;
            ((syntax_tree_node)to).source_context = new SourceContext(fp.line_num, fp.column_num, fp.line_num, fp.column_num, 0, 0);
        }

        public object sc_not_null(object o1, object o2)
        {
            if (o1 != null)
                if (((syntax_tree_node)o1).source_context != null) return o1;
            return o2;
        }

        public object sc_not_null(object o1, object o2, object o3)
        {
            if (o1 != null)
                if (((syntax_tree_node)o1).source_context != null) return o1;
            if (o2 != null)
                if (((syntax_tree_node)o2).source_context != null) return o2;
            return o3;
        }

        public object sc_not_null(params object[] arr)
        {
            foreach (object o in arr)
                if (o != null)
                    if (((syntax_tree_node)o).source_context != null) return o;
            return null;
        }

        public void assign_source_context(object to, object from)
        {
            //debug
            //if (((tree_node)from).source_context==null) Console.WriteLine("\n\rerror: from sc is null(assign_source_context)!\n\r");
            if (to != null && from != null)
                ((syntax_tree_node)to).source_context = ((syntax_tree_node)from).source_context;
        }
    }
}
