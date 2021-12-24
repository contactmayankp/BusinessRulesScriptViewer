using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sdmsols.XTB.BusinessRulesScriptViewer.Helpers
{
    public  class BusinessRuleTreeNode : TreeNode
    {
        public BusinessRuleProxy BusinessRuleProxy;
        public BusinessRuleTreeNode(BusinessRuleProxy br)
        {
            BusinessRuleProxy = br;
            this.Text = BusinessRuleProxy.Name;
        }
    }
}
