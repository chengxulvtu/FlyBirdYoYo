
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace FlyBirdYoYo.Utilities.SQL
{
    /// -----------------------------------------------------------------------------
    /// <summary>
    ///   The SqlUtils class provides Shared/Static methods for working with SQL Server related code
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// -----------------------------------------------------------------------------
    public static class SqlUtils
    {
        private static string[] DIRTY_SQL_CHARS = new string[] { "[", "]", "-", "\\", ";", "//", ",", "(", ")", "}", "{", "%", "@", "*", "!", "'", "|" , "1=1", "select *", "and'", "or'", "insert into", "delete from", "alter table", "update", "create table", "create view", "drop view", "creat eindex", "drop index", "create procedure", "drop procedure", "create trigger", "drop trigger", "create schema", "drop schema", "create domain", "alter domain", "drop domain", ");", "select@", "declare@", "print@" };
        /// <summary>
        /// ����Ƿ���SqlΣ���ַ�
        /// </summary>
        /// <param name="strInput">Ҫ�ж��ַ���</param>
        /// <returns>�жϽ��</returns>
        public static bool IsSafeSqlString(string strInput)
        {
            //���˵� tab  �س� ���з�
            strInput = strInput.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Replace(" ", "");

        
         
            foreach (string ss in DIRTY_SQL_CHARS)
            {
                if (strInput.IndexOf(ss) > 0)
                {
                    return false;
                }

            }

            return true;
        }

        public static string ToSafeSqlString(this string strInput)
        {
            return strInput.ToSafeSqlString("");
        }

        /// <summary>
        /// ת���ɰ�ȫ�ַ���
        /// </summary>
        /// <param name="strInput"></param>
        /// <param name="defaultString"></param>
        /// <returns></returns>
        public static string ToSafeSqlString(this string strInput, string defaultString)
        {
            if (strInput == null)
            {
                return "";
            }

            if (!IsSafeSqlString(strInput))
            {
                return FilterString(strInput, defaultString);
            }
            return strInput;
        }

        private static string FilterString(string strInput, string defaultString)
        {
            foreach (var itemDityChar in DIRTY_SQL_CHARS)
            {
                strInput.Replace(itemDityChar, defaultString);
            }

            return strInput;
        }


    }
}

