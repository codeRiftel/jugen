using System;
using System.Text;
using jar;
using System.Collections.Generic;

class Init {
    private class Core {
        public string src;
        public StringBuilder builder;
        public Dictionary<string, bool> refs;
    }

    private static int Main(string[] args) {
        string line;
        var inputBuilder = new StringBuilder();
        while ((line = Console.ReadLine()) != null) {
            inputBuilder.Append(line);
            inputBuilder.Append('\n');
        }

        var src = inputBuilder.ToString();

        if (args.Length < 1) return -1;

        var locs = new Loc[1 << 10];
        var len = Jar.Parse(src, locs, 0);
        if (locs[0].tok != Tok.Obj) return -1;
        int j = 1;
        var o = new StringBuilder();

        var refs = new Dictionary<string, bool>();
        while (j < locs[0].width) {
            var cls = locs[j++];
            var dsc = locs[j++];

            var clsName = src.Substring(cls.start, cls.len);
            refs[clsName] = false;

            var i = j;
            while (i < j + dsc.width) {
                var key = locs[i++];
                var val = locs[i++];

                if (key.len == 8 && string.Compare(src, key.start, "__is_ref", 0, 8) == 0) {
                    refs[clsName] = true;
                    break;
                }

                i += val.width;
            }

            j += dsc.width;
        }

        var core = new Core { src = src, builder = o, refs = refs };

        j = 1;

        int depth = 0;
        for (int i = 1; i < args.Length; i++) {
            o.Append("using ");
            o.Append(args[i]);
            o.Append(";\n");
        }
        o.Append("using jug;\n");
        o.Append("using rin;\n\n");
        o.Append("namespace ");
        o.Append(args[0]);
        o.Append(" {\n");

        var start = 3;
        while (j < locs[0].width) {
            var cls = locs[j++];
            var dsc = locs[j++];

            if (cls.len == 8 && string.Compare(src, cls.start, "__slaves", 0, 8) == 0) {
                if (j == 3) start = j + dsc.width + 2;
                j += dsc.width;
                continue;
            }

            if (j != start) o.Append("\n\n");

            Indent(o, ++depth);
            o.Append("public static class ");
            o.Append(src, cls.start, cls.len);
            o.Append("JUG");
            o.Append(" {\n");

            Indent(o, ++depth);
            o.Append("public static int Gen(");
            o.Append(src, cls.start, cls.len);
            o.Append(" obj, Ent[] e, int i, Rin rin) {\n");

            Indent(o, ++depth);
            o.Append("var root = i;\n");
            Indent(o, depth);
            o.Append("e[i++] = Ent.Obj(");
            var clsName = src.Substring(cls.start, cls.len);
            var size = dsc.size;
            if (refs.ContainsKey(clsName) && refs[clsName]) size -= 2;
            o.Append(size);
            o.Append(");\n");

            var i = j;
            while (i < j + dsc.width) {
                var key = locs[i++];
                var val = locs[i++];

                if (key.len == 8 && string.Compare(src, key.start, "__is_ref", 0, 8) == 0) {
                    i += val.width;
                    continue;
                }

                var keyName = src.Substring(key.start, key.len);
                depth = Gen(core, keyName, false, $"obj.{keyName}", val, "root", 2, depth);
                o.Append('\n');

                i += val.width;
            }

            Indent(o, depth);
            o.Append("return i;\n");

            Indent(o, --depth);
            o.Append("}\n");

            Indent(o, --depth);
            o.Append("}");
            depth--;

            j += dsc.width;
        }

        depth--;
        o.Append("\n}");
        Console.WriteLine(o.ToString());
        return 0;
    }

    private static int Gen(
        Core q,
        string key,
        bool unqKey,
        string obj,
        Loc v,
        string p,
        int rs,
        int d
    ) {
        var (src, o, refs) = (q.src, q.builder, q.refs);
        var isNull = v.len > 0 && src[v.start + v.len - 1] == '?';
        if (isNull) v.len--;
        var isStr = v.len == 6 && string.Compare(src, v.start, "string", 0, 4) == 0;
        var isBool = v.len == 4 && string.Compare(src, v.start, "bool", 0, 4) == 0;
        var isNum = IsNum(src, v.start, v.len);
        var lastTwo = v.start + v.len - 2;
        var isArr = v.len > 2 && string.Compare(src, lastTwo, "[]", 0, 2) == 0;
        var isList = v.len > 5 && string.Compare(src, v.start, "List<", 0, 5) == 0;
        var isDict = v.len > 19 && string.Compare(src, v.start, "Dictionary<string, ", 0, 19) == 0;
        var isEnum = v.len > 5 && string.Compare(src, v.start, "enum ", 0, 5) == 0;
        if (isEnum) v.start += 5;

        var valClsName = src.Substring(v.start, v.len);
        var isRef = refs.ContainsKey(valClsName) && refs[valClsName];

        var needGuard = isStr || isArr || isList || isRef || isNull || isDict;
        if (needGuard) {
            Indent(o, d);
            o.Append("if (");
            o.Append(obj);
            o.Append(" != null) {\n");
            d++;
        }

        if (key != null) {
            Indent(o, d);
            o.Append("e[i++] = Ent.Str(");
            if (!unqKey) o.Append('"');
            o.Append(key);
            if (!unqKey) o.Append('"');
            o.Append(");\n");
        }

        if (isBool) {
            Indent(o, d);
            o.Append("if (");
            o.Append(obj);
            if (isNull) o.Append(".Value");
            o.Append(") e[i++] = Ent.Pri(\"true\"); else e[i++] = Ent.Pri(\"false\");\n");
        } else if (isStr) {
            Indent(o, d);
            o.Append("e[i++] = Ent.Str(rin.Esc(");
            o.Append(obj);
            o.Append("));\n");
        } else if (isNum) {
            Indent(o, d);
            o.Append("e[i++] = Ent.Pri(Rin.To(");
            o.Append(obj);
            if (isNull) o.Append(".Value");
            o.Append("));\n");
        } else if ((isArr || isList) && key != null) {
            Indent(o, d);
            o.Append("var ");
            o.Append(key);
            o.Append("Root = i++;\n");
            Indent(o, d);
            o.Append("e[");
            o.Append(key);
            o.Append("Root] = Ent.Arr(");
            o.Append(obj);
            if (isArr) o.Append(".Length);\n"); else o.Append(".Count);\n");
            Indent(o, d);
            o.Append("for (int j = 0; j < ");
            o.Append(obj);
            if (isArr) o.Append(".Length"); else o.Append(".Count");
            o.Append("; j++) {\n");

            d++;

            var arrType = v;
            if (isArr) {
                arrType.size -= 2;
                arrType.len -= 2;
            } else {
                arrType.start += 5;
                arrType.len -= 6;
            }
            d = Gen(q, null, false, $"{obj}[j]", arrType, $"{key}Root", 1, d);

            Indent(o, --d);
            o.Append("}\n");
        } else if (isDict) {
            Indent(o, d);
            o.Append("var ");
            o.Append(key);
            o.Append("Root = i++;\n");
            Indent(o, d);
            o.Append("e[");
            o.Append(key);
            o.Append("Root");
            o.Append("] = Ent.Obj(");
            o.Append(obj);
            o.Append(".Count * 2);\n");
            Indent(o, d);
            o.Append("foreach (var pair in ");
            o.Append(obj);
            o.Append(") {\n");
            d++;
            var dictType = v;
            dictType.start += 19;
            dictType.len -= 20;
            d = Gen(q, "rin.Esc(pair.Key)", true, "pair.Value", dictType, $"{key}Root", 2, d);
            Indent(o, --d);
            o.Append("}\n");
        } else if (isEnum) {
            Indent(o, d);
            o.Append("e[i++] = Ent.Pri(Rin.To((int)");
            o.Append(obj);
            o.Append("));\n");
        } else {
            Indent(o, d);
            o.Append("i = ");
            o.Append(src, v.start, v.len);
            o.Append("JUG.Gen(");
            o.Append(obj);
            if (isNull) o.Append(".Value");
            o.Append(", e, i, rin);\n");
        }

        if (needGuard) {
            Indent(o, --d);
            o.Append("} else e[");
            o.Append(p);
            o.Append("]");
            o.Append(".size -= ");
            o.Append(rs);
            o.Append(";\n");
        }

        return d;
    }

    private static bool IsNum(string src, int start, int len) {
        switch (len) {
            case 3:
                return string.Compare(src, start, "int", 0, 3) == 0;
            case 4:
                if (string.Compare(src, start, "byte", 0, 4) == 0) {
                    return true;
                } else if (string.Compare(src, start, "uint", 0, 4) == 0) {
                    return true;
                } else if (string.Compare(src, start, "long", 0, 4) == 0) {
                    return true;
                }
                break;
            case 5:
                if (string.Compare(src, start, "sbyte", 0, 5) == 0) {
                    return true;
                } else if (string.Compare(src, start, "short", 0, 5) == 0) {
                    return true;
                } else if (string.Compare(src, start, "ulong", 0, 5) == 0) {
                    return true;
                } else if (string.Compare(src, start, "float", 0, 5) == 0) {
                    return true;
                }
                break;
            case 6:
                if (string.Compare(src, start, "ushort", 0, 6) == 0) {
                    return true;
                } else if (string.Compare(src, start, "double", 0, 6) == 0) {
                    return true;
                }
                break;
            case 7:
                return string.Compare(src, start, "decimal", 0, 7) == 0;
        }

        return false;
    }

    private static void Indent(StringBuilder o, int depth) {
        for (int i = 0; i < depth; i++) o.Append("    ");
    }
}
