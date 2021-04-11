using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = Null.Test.CSharpCodeFixVerifier<
    Null.NullAnalyzer,
    Null.NullCodeFixProvider>;

namespace Null.Test
{
    [TestClass]
    public class NullUnitTest
    {
        public async Task TestMethodFromMain(string mainFuncSrc, string mainFuncExpected)
        {
            var pre = @"
using System;

namespace ConsoleApplication1
{
    class Program
    {
";
            var post = @"
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(pre + mainFuncSrc + post, pre + mainFuncExpected + post);
        }

        [TestMethod]
        public async Task TestEmpty()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestIf()
        {
            await TestMethodFromMain(@"
        static void Main(string[] args)
        {
            [|if (args == null)
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }|]
        }",
                @"
        static void Main(string[] args)
        {
        }"
            );
        }

        [TestMethod]
        public async Task TestIfElse()
        {
            await TestMethodFromMain(@"
        static void Main(string[] args)
        {
            [|if (args == null)
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }|]
        }",
                @"
        static void Main(string[] args)
        {
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestValue()
        {
            await TestMethodFromMain(
                @"
        static void Main(int val)
        {
            if (val == null)
            {
            }
        }",
                @"
        static void Main(int val)
        {
            if (val == null)
            {
            }
        }"
            );
        }

        [TestMethod]
        public async Task TestInsideIf()
        {
            await TestMethodFromMain(
                @"
        static void Main(string[] args)
        {
            if (true)
                [|if (args == null)
                {
                    Console.Out.WriteLine(""ToBeRemoved"");
                }
                else
                {
                    Console.Out.WriteLine(""ToBeLeftAlone"");
                }|]
        }",
                @"
        static void Main(string[] args)
        {
            if (true)
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
        }"
            );
        }

        [TestMethod]
        public async Task TestInsideIfNoCurlyBraces()
        {
            await TestMethodFromMain(
                @"
        static void Main(string[] args)
        {
            if (true)
                [|if (args == null)
                    Console.Out.WriteLine(""ToBeRemoved"");
                else
                    Console.Out.WriteLine(""ToBeLeftAlone"");|]
        }",
                @"
        static void Main(string[] args)
        {
            if (true)
                Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestRemoveCurlyBraces()
        {
            await TestMethodFromMain(
                @"
        static void Main(string[] args)
        {
            [|if (args == null)
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
                Console.Out.WriteLine(""SomethingElse"");
            }|]
        }",
                @"
        static void Main(string[] args)
        {
            Console.Out.WriteLine(""ToBeLeftAlone"");
            Console.Out.WriteLine(""SomethingElse"");
        }"
            );
        }

        [TestMethod]
        public async Task TestCommutative()
        {
            await TestMethodFromMain(@"
        static void Main(string[] args)
        {
            [|if (null == args)
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }|]
        }",
                @"
        static void Main(string[] args)
        {
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestNeq()
        {
            await TestMethodFromMain(@"
        static void Main(string[] args)
        {
            [|if (args != null)
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }|]
        }",
                @"
        static void Main(string[] args)
        {
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestMethodArrow()
        {
            await TestMethodFromMain(@"
        static bool isNull(object input) => input == null;

        static void Main(string[] args)
        {
            [|if (isNull(args))
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }|]
        }",
                @"
        static bool isNull(object input) => input == null;

        static void Main(string[] args)
        {
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestMethodBody()
        {
            await TestMethodFromMain(@"
        static bool isNull(object input)
        {
            return input == null;
        }

        static void Main(string[] args)
        {
            [|if (isNull(args))
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }|]
        }",
                @"
        static bool isNull(object input)
        {
            return input == null;
        }

        static void Main(string[] args)
        {
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestFakeCall()
        {
            await TestMethodFromMain(@"
        static bool isNull(object input) => input == null;

        static void Main(string[] args)
        {
            object local = null;
            if (isNull(local))
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
        }",
                @"
        static bool isNull(object input) => input == null;

        static void Main(string[] args)
        {
            object local = null;
            if (isNull(local))
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
        }"
            );
        }

        [TestMethod]
        public async Task TestLogic()
        {
            await TestMethodFromMain(@"
        static void Main(string[] args, string arg1, string arg2)
        {
            int someVar = 0;
            [|if (args != null && arg1 != null && arg2 != null || someVar == 1)
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }|]
        }",
                @"
        static void Main(string[] args, string arg1, string arg2)
        {
            int someVar = 0;
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestMultiMethods()
        {
            await TestMethodFromMain(@"
        static bool isNull1(object input1) => input1 == null;
        static bool isNull2(object input2) => isNull3(input2);
        static bool isNull3(object input3) => isNull1(input3);
        static bool isNull4(object input4) => isNull2(input4);

        static void Main(string arg0, string arg1, string arg2, string arg3)
        {
            [|if (isNull1(arg0) || arg1 == null || isNull4(arg2))
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else [|if (isNull4(arg3))
            {
                Console.Out.WriteLine(""ToBeRemovedToo"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }|]|]
        }",
                @"
        static bool isNull1(object input1) => input1 == null;
        static bool isNull2(object input2) => isNull3(input2);
        static bool isNull3(object input3) => isNull1(input3);
        static bool isNull4(object input4) => isNull2(input4);

        static void Main(string arg0, string arg1, string arg2, string arg3)
        {
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }

        [TestMethod]
        public async Task TestStackOverflow()
        {
            await TestMethodFromMain(@"
        static bool isNull1(object input1) => isNull2(input1);
        static bool isNull2(object input2) => isNull1(input2);

        static void Main(string arg0, string arg1, string arg2, string arg3)
        {
            if (isNull1(arg0))
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
        }",
                @"
        static bool isNull1(object input1) => isNull2(input1);
        static bool isNull2(object input2) => isNull1(input2);

        static void Main(string arg0, string arg1, string arg2, string arg3)
        {
            if (isNull1(arg0))
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }
        }"
            );
        }

        [TestMethod]
        public async Task TestMultiMethodsMultiArgs()
        {
            await TestMethodFromMain(@"
        static bool isNull1(object v, object y) => v == null;
        static bool isNull2(object y, object t) => isNull3(t, y);
        static bool isNull3(object t, object y) => isNull1(t, y);
        static bool isNull4(object a, object b) => isNull2(a, b);

        static void Main(string arg0, string arg1, string arg2, string arg3)
        {
            string abc = ""abc"";
            [|if (isNull1(arg0, arg0) || arg1 == null || isNull4(abc, arg2))
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else [|if (isNull4(abc, arg3))
            {
                Console.Out.WriteLine(""ToBeRemovedToo"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"" + abc);
            }|]|]
        }",
                @"
        static bool isNull1(object v, object y) => v == null;
        static bool isNull2(object y, object t) => isNull3(t, y);
        static bool isNull3(object t, object y) => isNull1(t, y);
        static bool isNull4(object a, object b) => isNull2(a, b);

        static void Main(string arg0, string arg1, string arg2, string arg3)
        {
            string abc = ""abc"";
            Console.Out.WriteLine(""ToBeLeftAlone"" + abc);
        }"
            );
        }

        [TestMethod]
        public async Task TestMoreLogic()
        {
            await TestMethodFromMain(@"
        static void Main(string[] args, string arg1, string arg2)
        {
            int someVar = 0;
            [|if (!(args != null) && (arg1 != null || arg2 != null) && arg2 != null && !(someVar == 1))
            {
                Console.Out.WriteLine(""ToBeRemoved"");
            }
            else
            {
                Console.Out.WriteLine(""ToBeLeftAlone"");
            }|]
        }",
                @"
        static void Main(string[] args, string arg1, string arg2)
        {
            int someVar = 0;
            Console.Out.WriteLine(""ToBeLeftAlone"");
        }"
            );
        }
    }
}
