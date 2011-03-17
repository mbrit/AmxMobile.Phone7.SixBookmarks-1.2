using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AmxMobile.Phone7.SixBookmarks
{
    internal abstract class ODataServiceProxy : ServiceProxy
    {
        private const String AtomNamespace = "http://www.w3.org/2005/Atom";
        private const String MsMetadataNamespace = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        private const String MsDataNamespace = "http://schemas.microsoft.com/ado/2007/08/dataservices";

        protected ODataServiceProxy(string serviceName)
            : base(serviceName)
        {
        }

        public void GetAll<T>(Action<List<T>> callback, Failed failed)
            where T : Entity
        {
            EntityType et = EntityType.GetEntityType(typeof(T));

            // create a state object...
            ODataFetchState<T> state = new ODataFetchState<T>();
            state.Callback = callback;
            state.Failed = failed;

            // run...
            String url = GetServiceUrl(et);
            HttpHelper.DownloadXml(url, GetDownloadSettings(), new Action<XDocument>(state.ReceiveXml), failed);
        }

        private class ODataFetchState<T>
            where T : Entity
        {
            internal Action<List<T>> Callback;
            internal Failed Failed;

            internal void ReceiveXml(XDocument doc)
            {
                Debug.WriteLine("Received XML from server...");

                // got...
                List<T> items = LoadEntities(doc, EntityType.GetEntityType(typeof(T)));
                if (items == null)
                    throw new InvalidOperationException("'items' is null.");

                // debug...
                Debug.WriteLine(string.Format("{0} item(s) loaded.", items.Count));

                // callback...
                this.Callback(items);
            }

            protected List<T> LoadEntities(XDocument doc, EntityType et)
            {
                // feed...
                XElement feed = doc.Element(XName.Get("feed", AtomNamespace));
                if (feed == null)
                    throw new InvalidOperationException("'feedElement' is null.");

                // walk...
                List<T> results = et.CreateCollectionInstance<T>();
                var entries = feed.Elements(XName.Get("entry", AtomNamespace));
                foreach (XElement entry in entries)
                {
                    // get the content item...
                    XElement content = entry.Element(XName.Get("content", AtomNamespace));
                    if (content == null)
                        throw new InvalidOperationException("'content' is null.");

                    // then get the properties element...
                    XElement properties = content.Element(XName.Get("properties", MsMetadataNamespace));
                    if (properties == null)
                        throw new InvalidOperationException("'properties' is null.");

                    // create an item...
                    T item = (T)et.CreateInstance();
                    if (item == null)
                        throw new InvalidOperationException("'item' is null.");

                    // then get the fields...
                    Dictionary<string, object> values = new Dictionary<string, object>();
                    foreach (XElement fieldElement in properties.Elements())
                    {
                        if (fieldElement.Name.Namespace == MsDataNamespace)
                        {
                            // do we have that field?
                            EntityField field = et.GetField(fieldElement.Name.LocalName, false);
                            if (field != null)
                            {
                                // get the value...
                                object value = this.GetValue(fieldElement);
                                item.SetValue(field, value, SetReason.UserSet);
                            }
                        }
                    }

                    // add...
                    results.Add(item);
                }

                // return...
                return results;
            }

            private Object GetValue(XElement field)
            {
                // fields are provided with a data element, like this....
                // <d:BookmarkId m:type="Edm.Int32">1002</d:BookmarkId>

                // look up the type name...
                string typeName = null;
                XAttribute attr = field.Attribute(XName.Get("type", MsMetadataNamespace));
                if (attr != null)
                    typeName = attr.Value;

                // nothing?
                if (string.IsNullOrEmpty(typeName))
                    return XmlHelper.GetStringValue(field);
                else if (string.Compare(typeName, "Edm.Int32", StringComparison.InvariantCultureIgnoreCase) == 0)
                    return XmlHelper.GetInt32Value(field);
                else
                    throw new Exception(string.Format("Cannot handle '%s'.", typeName));
            }
        }

        private string GetServiceUrl(EntityType et)
        {
            return HttpHelper.CombineUrlParts(this.ResolvedServiceUrl, et.NativeName);
        }

        private string GetEntityUrlForPush(Entity entity, int serverId)
        {
            return string.Format("{0}({1})", GetServiceUrl(entity.EntityType), serverId);
        }

        public void PushDelete(Entity entity, int serverId, Action callback, Failed failed)
        {
            // get...
            string url = GetEntityUrlForPush(entity, serverId);
            ExecuteODataOperation(ODataOperation.Delete, url, null, callback, failed);
        }

        public void PushUpdate(Entity entity, int serverId, Action callback, Failed failed)
        {
            XDocument doc = new XDocument();

            // entry...
            XElement entryElement = new XElement(XName.Get("entry", AtomNamespace));
            doc.Add(entryElement);

            // content...
            XElement contentElement = new XElement(XName.Get("content", AtomNamespace));
            contentElement.Add(new XAttribute(XName.Get("type", string.Empty), "application/xml"));
            entryElement.Add(contentElement);

            // properties...
            XElement propertiesElement = new XElement(XName.Get("properties", MsMetadataNamespace));
            contentElement.Add(propertiesElement);

            // walk the fields...
            EntityType et = entity.EntityType;
            if (et == null)
                throw new InvalidOperationException("'et' is null.");
            foreach (EntityField field in et.Fields)
            {
                if (!(field.IsKey) && field.IsOnServer)
                {
                    // create...
                    XElement element = new XElement(XName.Get(field.Name, MsDataNamespace));
                    object value = entity.GetValue(field);
                    if (value != null)
                        element.Value = value.ToString();

                    // add...
                    propertiesElement.Add(element);
                }
            }

            // run...
            String url = null;
            ODataOperation op = ODataOperation.Update;
            String xmlAsString = doc.ToString();
            if (serverId != 0)
                url = GetEntityUrlForPush(entity, serverId);
            else
            {
                url = this.GetServiceUrl(et);
                op = ODataOperation.Insert;
            }

            // run...
            ExecuteODataOperation(op, url, xmlAsString, callback, failed);
        }

        public void PushInsert(Entity entity, Action callback, Failed failed)
        {
            // an insert is an update but with a different url...
            PushUpdate(entity, 0, callback, failed);
        }

        private void ExecuteODataOperation(ODataOperation opType, String url, String xml, Action callback, Failed failed)
        {
            // create the request...
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // set the method...
            if (opType == ODataOperation.Insert)
                request.Method = "POST";
            else if (opType == ODataOperation.Update)
                request.Method = "MERGE";
            else if (opType == ODataOperation.Delete)
                request.Method = "DELETE";
            else
                throw new NotSupportedException(string.Format("Cannot handle '{0}'.", opType));

            // headers... (including our special tokens)...
            DownloadSettings settings = this.GetDownloadSettings();
            foreach (string name in settings.ExtraHeaders.Keys)
                request.Headers[name] = settings.ExtraHeaders[name];

            // create a state object...
            ODataRequestState state = new ODataRequestState();
            state.Request = request;
            state.Callback = callback;
            state.Failed = failed;
            state.OutboundXml = xml;

            // do we have xml?
            if (!(string.IsNullOrEmpty(xml)))
            {
                byte[] bs = Encoding.UTF8.GetBytes(xml);
                request.ContentType = "application/atom+xml";
                request.BeginGetRequestStream(new AsyncCallback(HandleOutboundXmlRequest), state);
            }
            else
                request.BeginGetResponse(new AsyncCallback(HandleODataOperationResponse), state);
        }

        private class ODataRequestState
        {
            internal HttpWebRequest Request;
            internal Action Callback;
            internal Failed Failed;
            internal string OutboundXml;
        }

        private void HandleOutboundXmlRequest(IAsyncResult result)
        {
            // state...
            ODataRequestState state = (ODataRequestState)result.AsyncState;
            try
            {
                Stream stream = state.Request.EndGetRequestStream(result);
                if (stream == null)
                    throw new InvalidOperationException("'stream' is null.");
                using (stream)
                {
                    // send it...
                    StreamWriter writer = new StreamWriter(stream);
                    writer.Write(state.OutboundXml);
                    writer.Flush();
                }

                // ok... next...
                state.Request.BeginGetResponse(new AsyncCallback(HandleODataOperationResponse), state);
            }
            catch (Exception ex)
            {
                state.Failed(ex);
            }
        }


        private void HandleODataOperationResponse(IAsyncResult result)
        {
            // state...
            ODataRequestState state = (ODataRequestState)result.AsyncState;
            try
            {
                // unwrap...
                HttpWebResponse response = (HttpWebResponse)state.Request.EndGetResponse(result);
                if (response == null)
                    throw new InvalidOperationException("'response' is null.");

                // dispose the response...
                response.Close();

                // ok...
                state.Callback();
            }
            catch (WebException ex)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("An error occurred when making an OData request.");
                if (ex.Response is HttpWebResponse)
                {
                    using (Stream stream = ((HttpWebResponse)ex.Response).GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(stream);
                        builder.Append(reader.ReadToEnd());
                    }
                }

                // throw...
                throw new InvalidOperationException(builder.ToString(), ex);
            }
            catch (Exception ex)
            {
                state.Failed(ex);
            }
        }
    }
}
