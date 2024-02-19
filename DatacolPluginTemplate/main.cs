using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Specialized;
using System.IO;
using LowLevel;
using System.Data;
using System.Net;
using System.Threading;

namespace Plugin
{
    /// <summary>
    /// Простейшая реализация интерфейса плагина, сборку с этим классом мы будем подгружать динамически,
    /// главный проект не имеет на нее ссылок! Класс реализует интерфейс плагина, 
    /// для унифицированной работы со всеми плагинами
    /// а так же для того, чтобы можно было динамически найти класс в сборке.
    /// </summary>
    public class HandlerClass : PluginInterface.IPlugin
    {
        // Create a new Mutex. The creating thread does not own the 
        // Mutex. 
        public static int globalCounter = 0;

        /// <summary>
        /// Обработчик плагина
        /// </summary>
        /// <param name="parameters">Словарь параметров: ключ - имя параметра (string), 
        /// значение - содержимое параметра (object, который в зависимости от типа плагина (задается в parameters["type"])
        /// и ключа приводится к тому или иному типу) </param>
        /// <param name="error">Переменная (string), в которую возвращается ошибка работы плагина, 
        /// если таковая произошла. Если ошибки не произошло, данная переменная должна оставаться пустой строкой</param>
        /// <returns>Возвращаемое значение - это объект, который может иметь тот или иной тип,
        /// в зависимости от типа плагина (задается в  parameters["type"])</returns>
        public object pluginHandler(Dictionary<string, object> parameters, out string error)
        {
            try
            {
                error = "";
                ///Получает имя кампании из словаря parameters
                string campaignname = parameters["campaignname"].ToString();
                string type = parameters["type"].ToString();


                #region load_page_plugin (плагин загрузки страницы)
                if (extra.sc(type, "load_page_plugin"))
                {
                    //параметр ССЫЛКА на загружаемую страницу
                    string url = parameters["url"].ToString();
                    //параметр уровень вложенности загружаемой страницы
                    int nestinglevel = Convert.ToInt32(parameters["nestinglevel"].ToString());
                    //параметр реферер для загружаемой страницы
                    string referer = parameters["referer"].ToString();
                    //параметр флаг использования прокси при загрузке
                    bool useproxy = Convert.ToBoolean(parameters["useproxy"].ToString());
                    //параметр объект Загрузчик Datacol
                    DatacolHttp http = (DatacolHttp)parameters["datacolhttp"];
                    //параметр имя прокси чекера
                    string checkername = parameters["checkername"].ToString();
                    //параметр режим использования прокси (список или из прокси чекера)
                    string proxymode = parameters["proxymode"].ToString();
                    //параметр предопределенный прокси для загрузки страницы
                    WebProxy webproxy = (WebProxy)parameters["webproxy"];
                    //параметр предопределенная кодировка загружаемой страницы
                    string encoding = parameters["encoding"].ToString();
                    WebProxy usedProxy = new WebProxy();

                    Dictionary<string, object> outDictParams = new Dictionary<string, object>();

                    

                    #region Get Config Params
                    //параметр конфигурация плагина экспорта
                    string config = parameters["config"].ToString();

                    Dictionary<string, object> configParams = GetDictionaryParamsConfig(config);
                    //примеры параметров
                    //
                    //List<string> listParameter = (List<string>)configParams["list-parameter"];
                    //bool boolParameter =  Convert.ToInt32(configParams["bool-parameter"].ToString()) == 1;
                    //int intParameter = Convert.ToInt32(configParams["int-parameter"].ToString());
                    //string stringParameter = configParams["string-parameter"].ToString();
                    #endregion

                    //В переменную content получает код страницы, используя параметры полученные выше.
                    //На выходе словарь outDictParams, который получает код ответа сервера, время загрузки, location, использованную проксю(если использовалась).
                    string content = http.request(url, referer, out outDictParams, out error);


                    //возвращает код загруженной страницы
                    return "LOADED BY PLUGIN - " + content;
                }
                #endregion

                #region finalize_plugin (плагин финализации)
                if (extra.sc(type, "finalize_plugin"))
                {
                    //Используется для обнуления счетчиков, закрытия соединений с базой данных, удаления временных файлов ...    
                    globalCounter = 0;

                    //возвращаемое значение не используется
                }
                #endregion
            }
            catch (Exception exp)
            {
                error = exp.Message;
            }

            return "возвращаемое значение по умолчанию (для типов плагинов, у которых оно не используется)";
        }


        #region Plugin Specific Functions
       
        #endregion

        #region Служебные функции


        public static Dictionary<string, object> GetDictionaryParamsConfig(string config)
        {

            string filecontent = config;
            Dictionary<string, object> dictParams = new Dictionary<string, object>();
            try
            {
                MatchCollection parameters = Regex.Matches(filecontent, "<dc5par[^<>]*?type=\"([^<>]*?)\"[^<>]*?name=\"([^<>]*?)\"[^<>]*?>(.*?)</dc5par>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                string type = string.Empty;
                string name = string.Empty;
                string value = string.Empty;

                int paramInt = -1;

                List<string> paramList = new List<string>();

                foreach (Match param1 in parameters)
                {


                    type = param1.Groups[1].Value.Trim();
                    name = param1.Groups[2].Value.Trim();
                    value = param1.Groups[3].Value.Trim();
                    if (type == "list-string")
                    {

                        paramList = extra.GetAllLines(value, true);

                        dictParams.Add(name, paramList);
                    }
                    else if (type == "string")
                    {
                        dictParams.Add(name, value);
                    }
                    else if (type == "int")
                    {
                        paramInt = Convert.ToInt32(value);
                        dictParams.Add(name, paramInt);
                    }
                    else
                    {
                        throw new Exception("Тип параметра " + type + " в конфигурации не поддерживается");
                    }

                }

            }
            catch
            {

            }

            return dictParams;
        }


        public static Dictionary<string, object> GetDictionaryParams(string filename, string encoding = "")
        {
            Encoding encode = null;

            if (encoding == "")
            {
                encode = Encoding.Default;
            }
            else
            {
                encode = Encoding.GetEncoding(encoding);
            }
            string filecontent = string.Empty;
            Dictionary<string, object> dictParams = new Dictionary<string, object>();
            if (!File.Exists(filename))
            {
                return dictParams;
            }
            else
            {
                filecontent = File.ReadAllText(filename, encode);

                MatchCollection parameters = Regex.Matches(filecontent, "<dc5par[^<>]*?type=\"([^<>]*?)\"[^<>]*?name=\"([^<>]*?)\"[^<>]*?>(.*?)</dc5par>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                string type = string.Empty;
                string name = string.Empty;
                string value = string.Empty;

                int paramInt = -1;

                List<string> paramList = new List<string>();

                foreach (Match param1 in parameters)
                {


                    type = param1.Groups[1].Value.Trim();
                    name = param1.Groups[2].Value.Trim();
                    value = param1.Groups[3].Value.Trim();
                    if (type == "list-string")
                    {

                        paramList = extra.GetAllLines(value, true);

                        dictParams.Add(name, paramList);
                    }
                    else if (type == "string")
                    {
                        dictParams.Add(name, value);
                    }
                    else if (type == "int")
                    {
                        paramInt = Convert.ToInt32(value);
                        dictParams.Add(name, paramInt);
                    }
                    else
                    {
                        throw new Exception("Тип параметра " + type + " в файле конфигурации " + filename + "не поддерживается");
                    }

                }

            }
            return dictParams;
        }

        #endregion

        #region Методы и свойства необходимые, для соответствия PluginInterface (обычно не используются при создании плагина)

        public void Init()
        {
            //инициализация пока не нужна
        }

        public void Destroy()
        {
            //это тоже пока не надо
        }

        public string Name
        {
            get { return "PluginName"; }
        }

        public string Description
        {
            get { return "Описание текущего плагина"; }
        }

        #endregion
    }
}
