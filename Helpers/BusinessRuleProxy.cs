using System.Xml;
using Jsbeautifier;
using Microsoft.Xrm.Sdk;

namespace Sdmsols.XTB.BusinessRulesScriptViewer.Helpers
{
    public class BusinessRuleProxy
    {
        #region Public Fields

        public Entity BusinessRuleEntity;
        private Beautifier _beautifier;

        #endregion Public Fields

        #region Public Constructors

        public BusinessRuleProxy(Entity businessRuleEntity)
        {
            BusinessRuleEntity = businessRuleEntity;
            _beautifier = new Beautifier(new BeautifierOptions
            {
                BraceStyle = BraceStyle.Expand,
                BreakChainedMethods = false,
                EvalCode = true,
                IndentChar = '\t',
                IndentSize = 1,
                IndentWithTabs = true,
                JslintHappy = true,
                KeepArrayIndentation = true,
                KeepFunctionIndentation = true,
                MaxPreserveNewlines = 1,
                PreserveNewlines = true
            });
        }

        #endregion Public Constructors

        #region Public Properties

        public string Name => BusinessRuleEntity["name"].ToString();

        public string ClientData => BusinessRuleEntity.Contains("clientdata")
            ? BusinessRuleEntity["clientdata"].ToString()
            : "";

        #endregion Public Properties

        #region Public Methods

        public override string ToString()
        {
            return $"{Name}";
        }


        public string ClientCode
        {
            get
            {
                if (ClientData != null)
                {
                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(ClientData);

                    var clientCodeUnFormatted = xml.SelectSingleNode("/clientdata/clientcode");

                    if (clientCodeUnFormatted != null)
                    {
                        var formattedCode = _beautifier.Beautify(clientCodeUnFormatted.InnerText);
                        return formattedCode;
                    }
                }

                return string.Empty;
            }
        }

        #endregion Public Methods
    }
}