using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logging;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Sdmsols.XTB.BusinessRulesScriptViewer.Helpers;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace Sdmsols.XTB.BusinessRulesScriptViewer
{
    public partial class BusinessRulesScriptViewer : PluginControlBase,IGitHubPlugin, IPayPalPlugin, IMessageBusHost, IHelpPlugin, IStatusBarMessenger, IAboutPlugin
    {
        #region Constructor and Class Variables

        private Settings _mySettings;
        private List<EntityMetadataProxy> _entities;
        
        private EntityMetadataProxy _selectedEntity;

        

        public event EventHandler<MessageBusEventArgs> OnOutgoingMessage;
        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        
        public BusinessRulesScriptViewer()
        {
            InitializeComponent();
        }

        #endregion Constructor and Class Variables

        #region XrmToolBox Plug In Methods

        private void BusinessRulesScriptViewer_Load(object sender, EventArgs e)
        {
           // ShowInfoNotification("This is a notification that can lead to XrmToolBox repository", new Uri("https://github.com/MscrmTools/XrmToolBox"));

            // Loads or creates the settings for the plugin
            if (!SettingsManager.Instance.TryLoad(GetType(), out _mySettings))
            {
                _mySettings = new Settings();

                LogWarning("Settings not found => a new settings file has been created!");
            }
            else
            {
                LogInfo("Settings found and loaded");
            }
        }

        private void tsbClose_Click(object sender, EventArgs e)
        {
            CloseTool();
        }

        /// <summary>
        /// This event occurs when the connection has been updated in XrmToolBox
        /// </summary>
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (_mySettings != null && detail != null)
            {
                _mySettings.LastUsedOrganizationWebappUrl = detail.WebApplicationUrl;
                LogInfo("Connection has changed to: {0}", detail.WebApplicationUrl);
            }
        }

        private void BusinessRulesScriptViewer_ConnectionUpdated(object sender, ConnectionUpdatedEventArgs e)
        {
            var orgver = new Version(e.ConnectionDetail.OrganizationVersion);
            var orgok = orgver >= new Version(6, 0);

            if (orgok)
            {
                LoadSolutions();
                LoadEntities();
            }
            else
            {
                LogError("CRM version too old for Auto Number Manager");

                MessageBox.Show($"Auto Number feature was introduced in\nMicrosoft Dynamics 365 July 2017 (9.0)\nCurrent version is {orgver}\n\nPlease connect to a newer organization to use this cool tool.",
                    "Organization too old", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }

        /// <summary>
        /// This event occurs when the plugin is closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BusinessRulesScriptViewer_OnCloseTool(object sender, System.EventArgs e)
        {

            // Before leaving, save the settings
            SettingsManager.Instance.Save(GetType(), _mySettings);
        }



        #endregion XrmToolBox Plug In Methods
        
        #region Control Events

        private void cmbSolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            
            if (cmbSolution.SelectedItem != null)
            {
                FilterEntities();
            }
        }

       

        #endregion Control Events
        
        #region Private Helper Methods

        private void LoadSolutions()
        {
            cmbSolution.Items.Clear();
            cmbSolution.Enabled = false;
            WorkAsync(new WorkAsyncInfo("Loading solutions...",
                (eventargs) =>
                {
                    var qx = new QueryExpression("solution");
                    qx.ColumnSet.AddColumns("friendlyname", "uniquename");
                    qx.AddOrder("installedon", OrderType.Ascending);
                    //qx.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
                    qx.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
                    var lePub = qx.AddLink("publisher", "publisherid", "publisherid");
                    lePub.EntityAlias = "P";
                    lePub.Columns.AddColumns("customizationprefix");
                    eventargs.Result = Service.RetrieveMultiple(qx);
                })
            {
                PostWorkCallBack = (completedargs) =>
                {
                    if (completedargs.Error != null)
                    {
                        MessageBox.Show(completedargs.Error.Message);
                    }
                    else
                    {
                        if (completedargs.Result is EntityCollection)
                        {
                            var solutions = (EntityCollection)completedargs.Result;
                            var proxiedsolutions = solutions.Entities.Select(s => new SolutionProxy(s)).OrderBy(s => s.ToString());
                            cmbSolution.Items.AddRange(proxiedsolutions.ToArray());
                            cmbSolution.Enabled = true;
                        }
                    }
                    
                }
            });
        }

        private void LoadEntities()
        {
            _entities = new List<EntityMetadataProxy>();
            WorkAsync(new WorkAsyncInfo("Loading entities...",
                (eventargs) => { eventargs.Result = MetadataHelper.LoadEntities(Service); })
            {
                PostWorkCallBack = (completedargs) =>
                {
                    if (completedargs.Error != null)
                    {
                        MessageBox.Show(completedargs.Error.Message);
                    }
                    else
                    {
                        if (completedargs.Result is RetrieveMetadataChangesResponse)
                        {
                            var metaresponse = ((RetrieveMetadataChangesResponse)completedargs.Result).EntityMetadata;
                            _entities.AddRange(metaresponse
                                .Where(e => e.IsCustomizable.Value == true && e.IsIntersect.Value != true)
                                .Select(m => new EntityMetadataProxy(m))
                                .OrderBy(e => e.ToString()));
                        }
                    }

                }
            });
        }

        private void FilterEntities()
        {
            cmbEntities.Items.Clear();
            cmbEntities.Enabled = false;

            var solution = cmbSolution.SelectedItem as SolutionProxy;
            if (solution == null)
            {
                return;
            }
            
            WorkAsync(new WorkAsyncInfo("Filtering entities...",
                (eventargs) =>
                {
                    
                    var qx = new QueryExpression("solutioncomponent");
                    qx.ColumnSet.AddColumns("objectid");
                    qx.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1);
                    qx.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solution.Solution.Id);
                    eventargs.Result = Service.RetrieveMultiple(qx);
                })
            {
                PostWorkCallBack = (completedargs) =>
                {
                    if (completedargs.Error != null)
                    {
                        MessageBox.Show(completedargs.Error.Message);
                    }
                    else
                    {
                        if (completedargs.Result is EntityCollection)
                        {
                            var includedentities = (EntityCollection)completedargs.Result;
                            var filteredentities = _entities.Where(e => includedentities.Entities.Select(i => i["objectid"]).Contains(e.Metadata.MetadataId));
                            cmbEntities.Items.AddRange(filteredentities.ToArray());
                        }
                    }
                    cmbEntities.Enabled = true;
                }
            });
        }

        private void LoadBusinessRules()
        {
            
            WorkAsync(new WorkAsyncInfo("Loading BusinessRules...",
                (eventargs) =>
                {
                    var fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'> " +
                                   "   <entity name='workflow' > " +
                                   "     <attribute name='workflowid' /> " +
                                   "     <attribute name='name' /> " +
                                   "     <attribute name='category' /> " +
                                   "     <attribute name='clientdata' /> " +
                                   "     <attribute name='primaryentity' /> " +
                                   "     <attribute name='statecode' /> " +
                                   "     <attribute name='createdon' /> " +
                                   "     <attribute name='ownerid' /> " +
                                   "     <attribute name='owningbusinessunit' /> " +
                                   "     <attribute name='type' /> " +
                                   "     <order attribute='name' descending='false' /> " +
                                   "     <filter type='and'> " +
                                   "       <condition attribute='primaryentity' operator='eq' value='" +  _selectedEntity.EntityTypeCode + "' />  " +
                                   "       <condition attribute='category' operator='eq' value='2' /> "+
                                   "     </filter>  "+
                                   "   </entity> " +
                                   "</fetch>"; 
                    eventargs.Result = Service.RetrieveMultiple(new FetchExpression(fetchXml));
                })
            {
                PostWorkCallBack = (completedargs) =>
                {
                    if (completedargs.Error != null)
                    {
                        MessageBox.Show(completedargs.Error.Message);
                    }
                    else
                    {
                        if (completedargs.Result is EntityCollection)
                        {
                            var solutions = (EntityCollection)completedargs.Result;
                            var proxiedsolutions = solutions.Entities.Select(s => new BusinessRuleProxy(s))
                                .OrderBy(s => s.ToString()).ToList();
                            AddItemToChekListBox(proxiedsolutions);
                        }
                    }

                }
            });
        }

        protected void AddItemToChekListBox(List<BusinessRuleProxy> collection)
        {
            tvBusinessRule.Invoke((System.Windows.Forms.MethodInvoker)delegate ()
            {
                tvBusinessRule.Nodes.Clear();

                foreach (var userView in collection)
                {
                    tvBusinessRule.Nodes.Add(new BusinessRuleTreeNode(userView));
                }

            });
        }

    
     

        #endregion Private Helper Methods
        
        #region Logging And ProgressBar Methods
        delegate void SetStatusTextCallback(string text);

        delegate void AddProgressStepCallback();



        #endregion Logging And ProgressBar Methods


        #region Interface Members
        public void OnIncomingMessage(MessageBusEventArgs message)
        {
            //throw new NotImplementedException();
        }
        public string RepositoryName => "BusinessRulesScriptViewer";

        public string UserName => "contactmayankp";
        
        public string DonationDescription => "Auto Number Updater";
        public string EmailAccount => "mayank.pujara@gmail.com";

        public string HelpUrl => "https://mayankp.wordpress.com/2021/12/24/xrmtoolbox-new-tool-businessrulesscriptviewer/";


        #endregion


        public void ShowAboutDialog()
        {
           // throw new NotImplementedException();
        }

        private void tslAbout_Click(object sender, EventArgs e)
        {
            Process.Start(HelpUrl);
        }
        
        private void cmbEntities_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbEntities.SelectedItem != null)
            {
                _selectedEntity = (EntityMetadataProxy)cmbEntities.SelectedItem;

                LoadBusinessRules();
            }
        }

        
        private void tvBusinessRule_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var myNode = (BusinessRuleTreeNode)e.Node;

            rtBRScript.Text = myNode.BusinessRuleProxy.ClientCode;
        }

        private void btnSaveSelected_Click(object sender, EventArgs e)
        {  
            if (tvBusinessRule.SelectedNode != null)
            {   
                string path = SaveBusinessRuleScript(tvBusinessRule.SelectedNode);

                MessageBox.Show("Script is Saved Successfully at " + path);
            }

        }

        private string SaveBusinessRuleScript(TreeNode currentNode)
        {
            string currentPath = Directory.GetCurrentDirectory();
            var currentDirectory = currentPath + @"\BusinessRulesScriptViewerGeneratedScripts";
            if (!Directory.Exists(currentDirectory))
            {
                Directory.CreateDirectory(currentDirectory);
            }

            if (currentNode != null)
            {
                var myNode = (BusinessRuleTreeNode)currentNode;

                var code = myNode.BusinessRuleProxy.ClientCode;

                string fileName = myNode.BusinessRuleProxy.Name + ".js";
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                string fullPath = currentDirectory + @"\" + fileName;

                File.WriteAllLines(fullPath, new string[] { code });
                
            }

            return currentDirectory;
        }

        private void btnSaveAll_Click(object sender, EventArgs e)
        {
            var path = string.Empty;
            foreach (var currentNode in tvBusinessRule.Nodes)
            {
                path = SaveBusinessRuleScript((TreeNode) currentNode);
            }


            MessageBox.Show("Scripts is Saved Successfully at " + path);
        }
    }
}