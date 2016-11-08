using System;
using System.Collections.Generic;
using System.Text;

namespace CYQ.Data.Xml
{
    internal class XHtmlFormat
    {
        string html;
        public XHtmlFormat(string html)
        {
            this.html = html;
        }
        private bool IsEnChar(char c)//Ӣ����ĸ
        {
            return (c > 64 && c < 91) || (c > 96 && c < 123);
        }
        public override string ToString()
        {
            //<input type="button" value="abcd" onclick="javascript:alert('<script>
            int labedState=-1;//0 Tag 1 Tag���� 2������ǩ���� 3��˫��ǩ����
            bool labelStart, keyStart, valueStart, slashStart,quoteStart;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < html.Length; i++)
            {
                char c = html[i];
                switch (c)
                {
                    case "<":
                        if (!keyStart && !valueStart)
                        {
                            labedState=0;
                        }
                        break;
                    case " ":
                        if(labelStart==0)
                        {
                            labelStart=1;
                        }
                    case ">":
                        if (!keyStart && !valueStart)
                        {
                            labelStart = true;
                        }
                    case "\"":
                        quoteStart = true;
                        if(
                    case "\\":
                        break;
                    default:

                        break;
                }
                sb.Append(c);
            }
        }
    }
}
