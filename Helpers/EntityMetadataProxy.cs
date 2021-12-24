using Microsoft.Xrm.Sdk.Metadata;

namespace Sdmsols.XTB.BusinessRulesScriptViewer.Helpers
{
    internal class EntityMetadataProxy
    {
        #region Public Fields

        public EntityMetadata Metadata;

        #endregion Public Fields

        #region Public Constructors

        public EntityMetadataProxy(EntityMetadata entityMetadata)
        {
            Metadata = entityMetadata;
        }

        #endregion Public Constructors

        #region Public Methods

        public string EntityTypeCode =>
            Metadata.ObjectTypeCode.HasValue ? Metadata.ObjectTypeCode.ToString() : string.Empty;

        public override string ToString()
        {
            if (Metadata != null)
            {
                if (!string.IsNullOrEmpty(Metadata?.DisplayName?.UserLocalizedLabel?.Label))
                {
                    return $"{Metadata.DisplayName.UserLocalizedLabel.Label} ({Metadata.LogicalName})";
                }
                return Metadata.LogicalName;
            }
            return base.ToString();
        }



        #endregion Public Methods
    }
}