using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using FlyBirdYoYo.Utilities.TypeFinder;
using FlyBirdYoYo.Utilities;
using FlyBirdYoYo.DbManage.Utilities;
using FlyBirdYoYo.DbManage.CommandTree;
using System.Reflection;

namespace FlyBirdYoYo.DbManage
{


    /// <summary>
    /// �������ݿ��  ������  ����ִ�������ݿ���н���
    /// </summary>
    public class SqlDbContext<TElement> : BaseSqlOperation<TElement>, IDbContext<TElement>, IDisposable
        where TElement : BaseEntity, new()
    {
        #region Construction and fields




        /// <summary>
        /// ʵ�����������
        /// </summary>
        private string EntityIdentityFiledName = new TElement().GetIdentity().IdentityKeyName;



        /// <summary>
        /// ���������� ���캯��
        /// </summary>
        /// <param name="dbConfig"></param>
        public SqlDbContext(DbConnConfig dbConfig)
        {
            this.DbConfig = dbConfig;
        }



        #endregion


        #region Context methods


        #region  Insert����
        /// <summary>
        /// ���� ʵ��
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public long Insert(TElement entity, IDbTransaction transaction = null)
        {
            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            ResolveEntity(entity, true, out tableInDbName, out propertys, out filelds, out paras);

            ///��������������
            var noIdentityPropertys = propertys.Remove(x => x.Name == EntityIdentityFiledName);
            var noIdentityFileds = filelds.Remove(x => x == EntityIdentityFiledName);
            var noIdentityParas = paras.Remove(x => x.ToLower() == string.Format("@{0}", EntityIdentityFiledName.ToLower()));

            var fieldSplitString = String.Join(",", noIdentityFileds);//���ض��ŷָ����ַ��� ���磺ProvinceCode,ProvinceName,Submmary
            var parasSplitString = String.Join(",", noIdentityParas);//����   ���� �Ķ��ŷָ�


            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.Append(string.Format("insert into {0}(", tableInDbName));
            sb_Sql.Append(string.Format("{0})", fieldSplitString));
            sb_Sql.Append(" values (");
            sb_Sql.Append(string.Format("{0})", parasSplitString));
            sb_Sql.Append(";select @@IDENTITY;");


            var sqlCmd = sb_Sql.ToString();


            ///������ַ���ƴ�ӹ�����
            sb_Sql.Clear();
            sb_Sql = null;

            this.SqlOutPutToLogAsync(sqlCmd, entity);

            using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
            {
                var result = conn.ExecuteScalar<long>(sqlCmd, entity, transaction);
                return result;
            }
        }



        /// <summary>
        /// ����������β�����ʵ��
        /// (ע�⣺sqlbuck���룬��Ч��sqlbuck��ʽ����)
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public bool InsertMulitiEntities(IEnumerable<TElement> entities, IDbTransaction transaction = null)
        {
            var result = -1;


            var count_entities = entities.Count();
            if (count_entities <= 0)
            {
                return false;
            }


            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            ResolveEntity(entities.First(), true, out tableInDbName, out propertys, out filelds, out paras);

            try
            {

                this.SqlOutPutToLogAsync("InsertMulitiEntities", entities);

                ///��������������
                var noIdentityPropertys = propertys.Remove(x => x.Name.ToLower() == EntityIdentityFiledName.ToLower());

                using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }



                    if (null == transaction)
                    {
                        transaction = conn.BeginTransaction();
                    }

                    using (var bulk = new SqlBulkCopy(conn as SqlConnection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction))
                    {
                        bulk.BulkCopyTimeout = 120;//���ʱʱ��
                        bulk.BatchSize = 1000;
                        //ָ��д���Ŀ���
                        bulk.DestinationTableName = tableInDbName;
                        //����Դ�е�������Ŀ�������Ե�ӳ���ϵ
                        //bulk.ColumnMappings.Add("ip", "ip");
                        //bulk.ColumnMappings.Add("port", "port");
                        //bulk.ColumnMappings.Add("proto_name", "proto_name");
                        //bulk.ColumnMappings.Add("strategy_id", "strategy_id");
                        //init mapping
                        foreach (var pi in noIdentityPropertys)
                        {
                            bulk.ColumnMappings.Add(pi.Name, pi.Name);
                        }

                        DataTable dt = SqlDataTableExtensions.ConvertListToDataTable<TElement>(entities, ref noIdentityPropertys);//����Դ����

                        //DbDataReader reader = dt.CreateDataReader();
                        bulk.WriteToServer(dt);

                        if (null != transaction)
                        {
                            transaction.Commit();
                        }
                    }

                }


                result = 1;

            }
            catch (Exception ex)
            {

                if (null != transaction)
                {
                    transaction.Rollback();
                }
                //�׳�Native �쳣��Ϣ
                throw ex;
            }


            var isSuccess = result > 0 ? true : false;


            return isSuccess;


        }

        #endregion


        #region Update ���²���

        /// <summary>
        /// ���µ���ģ��
        /// �����»���Ϊ��ģ���������õ�ֵ���ֶλᱻ���µ���������ֵ �����£�
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public int Update(TElement entity, IDbTransaction transaction = null)
        {
            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            var sqlFieldMapping = ResolveEntity(entity, true, out tableInDbName, out propertys, out filelds, out paras);
            if (filelds.Length <= 1)
            {
                //�������� û�������ֶ�
                return -1;
                throw new Exception("δָ���������������ֶΣ�");
            }

            StringBuilder sb_FiledParaPairs = new StringBuilder("");


            var settedValueDic = entity.GetSettedValuePropertyDic();

            foreach (var item in settedValueDic)
            {
                var keyProperty = item.Key;
                //var value = item.Value;
                if (keyProperty != EntityIdentityFiledName)
                {
                    string fieldName = ResolveLambdaTreeToCondition.SearchPropertyMappingField(sqlFieldMapping, keyProperty);
                    sb_FiledParaPairs.AppendFormat("{1}{0}{1}=@{2},", fieldName, this.FieldWrapperChar, keyProperty);
                }
            }

            //�Ƴ����һ������
            var str_FiledParaPairs = sb_FiledParaPairs.ToString();
            str_FiledParaPairs = str_FiledParaPairs.Remove(str_FiledParaPairs.Length - 1);

            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.Append(string.Format("update {0} set ", tableInDbName));//Set Table
            sb_Sql.Append(str_FiledParaPairs);//������

            sb_Sql.AppendFormat(" where {0}=@{0}", EntityIdentityFiledName);//����


            var sqlCmd = sb_Sql.ToString();
            ///������ַ���ƴ�ӹ�����
            sb_FiledParaPairs.Clear();
            sb_FiledParaPairs = null;
            sb_Sql.Clear();
            sb_Sql = null;

            this.SqlOutPutToLogAsync(sqlCmd, entity);

            using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
            {
                var result = conn.Execute(sqlCmd, entity, transaction);
                return result;
            }
        }

        /// <summary>
        /// ����Ԫ�� ͨ��  ����������
        /// �����»���Ϊ��ģ���������õ�ֵ���ֶλᱻ���µ���������ֵ �����£�
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="predicate"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public int UpdateByCondition(TElement entity, Expression<Func<TElement, bool>> predicate, IDbTransaction transaction = null)
        {
            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            var sqlFieldMapping = ResolveEntity(entity, true, out tableInDbName, out propertys, out filelds, out paras);
            if (filelds.Length <= 1)
            {
                //�������� û�������ֶ�
                return -1;
                throw new Exception("δָ���������������ֶΣ�");
            }


            StringBuilder sb_FiledParaPairs = new StringBuilder("");
            ///����Ҫ���µ���
            var settedValueDic = entity.GetSettedValuePropertyDic();

            foreach (var item in settedValueDic)
            {
                var keyProperty = item.Key;
                //var value = item.Value;
                if (keyProperty != EntityIdentityFiledName)
                {
                    string fieldName = ResolveLambdaTreeToCondition.SearchPropertyMappingField(sqlFieldMapping, keyProperty);
                    sb_FiledParaPairs.AppendFormat("{1}{0}{1}=@{2},", fieldName, this.FieldWrapperChar, keyProperty);
                }
            }
            //�Ƴ����һ������
            var str_FiledParaPairs = sb_FiledParaPairs.ToString();
            str_FiledParaPairs = str_FiledParaPairs.Remove(str_FiledParaPairs.Length - 1);

            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.Append(string.Format("update {0} set ", tableInDbName));//Set Table
            sb_Sql.Append(str_FiledParaPairs);//������



            if (null != predicate)
            {
                string where = ResolveLambdaTreeToCondition.ConvertLambdaToCondition<TElement>(predicate, sqlFieldMapping);
                sb_Sql.Append(" where ");//��������
                sb_Sql.Append(where);//�����д��в���=ֵ��  ƴ���ַ���
            }


            var sqlCmd = sb_Sql.ToString();

            ///�����ַ�������
            sb_FiledParaPairs.Clear();
            sb_FiledParaPairs = null;
            sb_Sql.Clear();
            sb_Sql = null;

            this.SqlOutPutToLogAsync(sqlCmd, entity);

            using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
            {
                var result = conn.Execute(sqlCmd, entity, transaction);
                return result;
            }
        }

        #endregion


        #region Select   ��ѯ����

        /// <summary>
        /// ͨ��������ȡ����Ԫ��
        /// </summary>
        /// <param name="id">Identifier</param>
        /// <returns>Entity</returns>
        public TElement GetElementById(long id)
        {

            TElement entity = new TElement();

            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            var sqlFieldMapping = ResolveEntity(entity, false, out tableInDbName, out propertys, out filelds, out paras);
            if (filelds.Length <= 1)
            {
                //�������� û�������ֶ�
                return null;
                throw new Exception("δָ���������������ֶΣ�");
            }
            //��ȡ�ֶ�
            //List<string> fieldAlias = new List<string>();
            //foreach (var item in sqlFieldMapping.Filelds)
            //{
            //    var ailasName = string.Format("{0} as {1}", item.FieldColumnName, item.PropertyName);
            //    fieldAlias.Add(ailasName);
            //}
            var fieldSplitString = "*";//entity.GetSqlQueryFieldsWithAlias();// String.Join(",", fieldAlias);//���ض��ŷָ����ַ��� ���磺ProvinceCode,ProvinceName,Submmary

            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.AppendFormat("select {0} ", fieldSplitString);
            sb_Sql.AppendFormat(" from {0} ", tableInDbName);//WITH (NOLOCK) ���ڲ�������ִ�е�������-�����������
            sb_Sql.AppendFormat(" where {0}={1};", EntityIdentityFiledName, id);

            var sqlCmd = sb_Sql.ToString();

            sb_Sql.Clear();
            sb_Sql = null;

            try
            {
                this.SqlOutPutToLogAsync(sqlCmd);
                using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
                {
                    entity = conn.QueryFirstOrDefault<TElement>(sqlCmd);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return entity;
        }

        /// <summary>
        /// ͨ���ض���������ѯ��Ԫ��
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public TElement GetFirstOrDefaultByCondition(Expression<Func<TElement, bool>> predicate)
        {
            TElement entity = new TElement();

            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            var sqlFieldMapping = ResolveEntity(entity, false, out tableInDbName, out propertys, out filelds, out paras);
            if (filelds.Length <= 1)
            {
                //�������� û�������ֶ�
                return null;
                throw new Exception("δָ���������������ֶΣ�");
            }
            //��ȡ�ֶ�
            //List<string> fieldAlias = new List<string>();
            //foreach (var item in sqlFieldMapping.Filelds)
            //{
            //    var ailasName = string.Format("{0} as {1}", item.FieldColumnName, item.PropertyName);
            //    fieldAlias.Add(ailasName);
            //}
            //��ȡ�ֶ�
            var fieldSplitString = "*";// entity.GetSqlQueryFieldsWithAlias();//String.Join(",", fieldAlias);//���ض��ŷָ����ַ��� ���磺ProvinceCode,ProvinceName,Submmary


            //������ѯ����
            string whereStr = "1=1";
            if (null != predicate)
            {
                whereStr = ResolveLambdaTreeToCondition.ConvertLambdaToCondition<TElement>(predicate, sqlFieldMapping);
            }



            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.AppendFormat("select top 1  {0} ", fieldSplitString);
            sb_Sql.AppendFormat(" from {0} ", tableInDbName);
            sb_Sql.AppendFormat(" where {0};", whereStr);


            var sqlCmd = sb_Sql.ToString();

            sb_Sql.Clear();
            sb_Sql = null;

            try
            {
                this.SqlOutPutToLogAsync(sqlCmd);
                using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
                {
                    entity = conn.QueryFirstOrDefault<TElement>(sqlCmd);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return entity;
        }

        /// <summary>
        /// ͨ���ض���������ѯ��Ԫ�ؼ���
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public List<TElement> GetElementsByCondition(Expression<Func<TElement, bool>> predicate)
        {
            TElement entity = new TElement();

            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            var sqlFieldMapping = ResolveEntity(entity, false, out tableInDbName, out propertys, out filelds, out paras);
            if (filelds.Length <= 1)
            {
                //�������� û�������ֶ�
                return null;
                throw new Exception("δָ���������������ֶΣ�");
            }
            //��ȡ�ֶ�
            //List<string> fieldAlias = new List<string>();
            //foreach (var item in sqlFieldMapping.Filelds)
            //{
            //    var ailasName = string.Format("{0} as {1}", item.FieldColumnName, item.PropertyName);
            //    fieldAlias.Add(ailasName);
            //}
            //��ȡ�ֶ�
            var fieldSplitString = "*";//entity.GetSqlQueryFieldsWithAlias();// String.Join(",", fieldAlias);//���ض��ŷָ����ַ��� ���磺ProvinceCode,ProvinceName,Submmary


            //������ѯ����
            string whereStr = "1=1";
            if (null != predicate)
            {
                whereStr = ResolveLambdaTreeToCondition.ConvertLambdaToCondition<TElement>(predicate, sqlFieldMapping);
            }



            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.AppendFormat("select  {0} ", fieldSplitString);
            sb_Sql.AppendFormat(" from {0} ", tableInDbName);
            sb_Sql.AppendFormat(" where {0};", whereStr);


            var sqlCmd = sb_Sql.ToString();

            sb_Sql.Clear();
            sb_Sql = null;

            List<TElement> dataLst = null;
            try
            {
                this.SqlOutPutToLogAsync(sqlCmd);

                using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
                {
                    dataLst = conn.Query<TElement>(sqlCmd).AsList();
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return dataLst;
        }



        /// <summary>
        /// ִ�з�ҳ��ѯ�ĺ��ķ�����֧�ֵ���Ͷ���ҳ
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="condition">ִ������</param>
        /// <returns></returns>
        public override PagedSqlDataResult<T> PageQuery<T>(PagedSqlCondition condition)
        {
            PagedSqlDataResult<T> pageData = new PagedSqlDataResult<T>(); ;
            if (null == condition)
            {
                return pageData;
            }
            string errMsg = "";
            if (!condition.IsValid(out errMsg))
            {
                throw new Exception("��ҳ��ѯ����" + errMsg);
            }

       

            try
            {


                //�жϽ��������
                if (null == condition.TableOptions)
                {
                    //��sql��������ж�
                    if (condition.TableNameOrSqlCmd.ToLower().Contains("select") && condition.TableNameOrSqlCmd.ToLower().Contains("from"))
                    {
                        condition.TableOptions = PageTableOptions.SqlScripts;
                    }
                    else
                    {
                        condition.TableOptions = PageTableOptions.TableOrView;
                    }
                }

                //��̬��ѯ��Ҫ��װ���
                if (condition.TableOptions == PageTableOptions.SqlScripts)
                {
                    //condition.TableNameOrSqlCmd = condition.TableNameOrSqlCmd.Replace("'", "''");
                    condition.TableNameOrSqlCmd = string.Format(" ( {0} ) as  tmpTable ", condition.TableNameOrSqlCmd);
                }

                //���÷�ҳ�洢����
                StringBuilder sb_Sql = new StringBuilder();
                sb_Sql.Append(Contanst.PageSql_Call_Name);

                var sqlCmd = sb_Sql.ToString();

                var sqlParas = new DynamicParameters();
                sqlParas.Add("@PageIndex", condition.PageNumber - 1);//ҳ����
                sqlParas.Add("@PageSize", condition.PageSize);//ҳ��С
                sqlParas.Add("@TableName", condition.TableNameOrSqlCmd);//������
                sqlParas.Add("@SelectFields", condition.SelectFields);//��ѯ���ֶ�
                ////sqlParas.Add("@PrimaryKey", condition.PrimaryKey);//��ѯ�ı������
                sqlParas.Add("@ConditionWhere", condition.ConditionWhere);//��ѯ����      
                sqlParas.Add("@SortField", condition.SortField);//�����ֶ�
                sqlParas.Add("@IsDesc", condition.IsDesc == true ? 1 : 0);//������ ������
                sqlParas.Add("@TotalRecords", DbType.Int32, direction: ParameterDirection.Output);//�ܼ�¼������ѡ������
                sqlParas.Add("@TotalPageCount", DbType.Int32, direction: ParameterDirection.Output);//��ҳ�����������


                //��¼�����־
                this.SqlOutPutToLogAsync(sqlCmd, sqlParas);

                using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
                {
                    var dataList = conn.Query<T>(sqlCmd, sqlParas, commandType: CommandType.StoredProcedure).AsList();

                  
                    pageData.DataList = dataList;
                }

                //��ѯ��Ϻ� ����������� �����ܼ�¼�� ��ҳ��
                var totalRecords = sqlParas.Get<int>("@TotalRecords");
                var totalPages = sqlParas.Get<int>("@TotalPageCount");

                //��ѯ��Ϻ� ����������� �����ܼ�¼�� ��ҳ��
                pageData.TotalRows = totalRecords;
                pageData.TotalPages = totalPages;

            }
            catch (Exception ex)
            {
                throw ex;
            }
            return pageData;
        }

        #endregion


        #region Delete ɾ������

        /// <summary>
        /// ɾ��һ��ʵ��
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public int Delete(TElement entity, IDbTransaction transaction = null)
        {
            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            var sqlFieldMapping = ResolveEntity(entity, true, out tableInDbName, out propertys, out filelds, out paras);

            var identityKey = sqlFieldMapping.Filelds.Where(x => x.FieldColumnName == EntityIdentityFiledName).FirstOrDefault();
            if (null == identityKey)
            {
                //�������� û�������ֶ�
                return -1;
                throw new Exception("δָ�������ֶΣ�");
            }


            var primaryValue = ReflectionHelper.GetPropertyValue(entity, identityKey.PropertyName);

            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.AppendFormat("delete from {0} ", tableInDbName);
            sb_Sql.AppendFormat(" where {0}={1};", EntityIdentityFiledName, primaryValue);


            var sqlCmd = sb_Sql.ToString();

            //��������
            sb_Sql.Clear();
            sb_Sql = null;

            try
            {
                this.SqlOutPutToLogAsync(sqlCmd, entity);

                using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
                {
                    var result = conn.Execute(sqlCmd, transaction);
                    return result;
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// ɾ������������ʵ��
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public int DeleteByCondition(Expression<Func<TElement, bool>> predicate, IDbTransaction transaction = null)
        {
            TElement entity = new TElement();


            string tableInDbName;
            System.Reflection.PropertyInfo[] propertys;
            string[] filelds;
            string[] paras;
            var sqlFieldMapping = ResolveEntity(entity, true, out tableInDbName, out propertys, out filelds, out paras);
            if (filelds.Length <= 1)
            {
                //�������� û�������ֶ�
                return -1;
                throw new Exception("δָ���������������ֶΣ�");
            }

            //������ѯ����
            var whereStr = "1=1";
            if (null != predicate)
            {
                whereStr = ResolveLambdaTreeToCondition.ConvertLambdaToCondition<TElement>(predicate, sqlFieldMapping);
            }
            StringBuilder sb_Sql = new StringBuilder();
            sb_Sql.AppendFormat("delete from {0} ", tableInDbName);
            if (null != predicate)
            {
                sb_Sql.AppendFormat("where  {0}  ", whereStr);
            }

            var sqlCmd = sb_Sql.ToString();
            try
            {
                this.SqlOutPutToLogAsync(sqlCmd);

                using (var conn = DatabaseFactory.GetDbConnection(this.DbConfig))
                {
                    var result = conn.Execute(sqlCmd, transaction);
                    return result;
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }


        }



        #endregion


        /// <summary>
        /// ���ָ���Ĳ���ʵ�壬���г�ȡ�ض��Ĳ���ռλ��������
        /// EXEC SP_EXECUTESQL @sql,N'@Nums INT OUT,@Score INT',@OUT_Nums OUTPUT,@IN_Score
        /// �����м�� �����������֣�N'@Nums INT OUT,@Score INT'
        /// </summary>
        /// <param name="paraObj"></param>
        /// <param name="paraTokens"></param>
        /// <param name="paraKeyValueString"></param>
        /// <returns></returns>
        private string GetSqlServerParamDefineString(object paraObj, string[] paraTokens, out string paraKeyValueString)
        {
            StringBuilder sb_Para = new StringBuilder();
            StringBuilder sb_KeyValue = new StringBuilder(",");
            paraKeyValueString = string.Empty;


            if (paraTokens.IsEmpty())
            {
                return null;
            }

            var paraType = paraObj.GetType().GetTypeInfo();

            //ȥ���������ֶλ�������
            var arr_PubProps = paraType.DeclaredProperties;

            try
            {


                int counter = 0;
                foreach (var item in paraTokens)
                {
                    string pureName = item.Substring(1);//ȡ@֮�����Ϊ������



                    PropertyInfo propty = arr_PubProps.FirstOrDefault(m => m.Name.ToLower().Equals(pureName.ToLower()));
                    if (null == propty)
                    {
                        throw new Exception("��������ѯȱ�ٲ�������������" + item);
                    }

                    //����type ��ȡsqlserver��Ӧ���ֶ�����
                    string sqlDef = DbTypeAndCLRType.GetSqlServerDbDefine(propty.PropertyType);
                    object value = ReflectionHelper.FastGetValue(propty, paraObj);



                    //���� guid  �ַ������� ���Ű���
                    if (value is string || value is DateTime || value is char || value is Guid)
                    {
                        if (value is DateTime)
                        {
                            //��ʱ�����ͳһ yyyy-MM-dd HH:mm:ss
                            value = string.Format("'{0}'", ((DateTime)value).ToOfenTimeString());
                        }
                        value = string.Format("'{0}'", value.ToString());
                    }
                    else if (value is bool)
                    {
                        //bool ���⴦��
                        if ((bool)value == true)
                        {
                            value = 1;
                        }
                        else
                        {
                            value = 0;
                        }
                    }

                    if (sqlDef.IsNotEmpty())
                    {
                        if (counter == 0)
                        {
                            sb_Para.Append(item).Append(" ").Append(sqlDef);//�磺@a int

                            sb_KeyValue.Append(item).Append(" = ").Append(value);
                        }
                        else
                        {
                            sb_Para.Append(",").Append(item).Append(" ").Append(sqlDef);//�磺,@b nvarchar

                            sb_KeyValue.Append(",").Append(item).Append(" = ").Append(value);
                        }

                    }

                    counter += 1;
                }



            }
            catch (Exception ex)
            {
                throw ex;
            }

            paraKeyValueString = sb_KeyValue.ToString();

            return sb_Para.ToString();
        }

        #region Disposable


        //�Ƿ�������
        bool _disposed;
        public void Dispose()
        {

            Dispose(true);
            // This class has no unmanaged resources but it is possible that somebody could add some in a subclass.
            GC.SuppressFinalize(this);

        }
        //����Ĳ�����ʾʾ�Ƿ���Ҫ�ͷ���Щʵ��IDisposable�ӿڵ��йܶ���
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return; //����Ѿ������գ����ж�ִ��
            if (disposing)
            {
                //TODO:�ͷ���Щʵ��IDisposable�ӿڵ��йܶ���

            }
            //TODO:�ͷŷ��й���Դ�����ö���Ϊnull
            _disposed = true;
        }


        #endregion


        #endregion

    }
}
