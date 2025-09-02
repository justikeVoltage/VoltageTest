/****************************************************
    功能：设置后点击按钮自动获取对应的ObiParticleAttachment组件引用
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;
using Autohand;
using System;
using NaughtyAttributes;
using System.Threading;

namespace Voltage
{
    public class ObiControlBase : MonoBehaviour
    {
        [Header("ObiParticleAttachment Acquisition"), Space(3)]
        [InfoBox("You can automatically obtain c through the settings of a and b (button below or right-click on the program)", EInfoBoxType.Normal)]
        [SerializeField, Tooltip("Particle attachment target")]
        private Transform m_attachmentTarget;

        [SerializeField, Tooltip("Object containing component ObiParticleAttachment")]
        public List<ObiActor> m_particleAttachmentTransforms = null;

        [SerializeField, Tooltip("Click the button below to get")]
        public List<ObiParticleAttachment> m_particleAttachmentList = null;


        [Space(3), Header("Obi Particle processing")]
        [SerializeField] private bool m_changeAttachmentType = true;
        
        public virtual void Awake()
        {
            if (m_particleAttachmentList == null)
            {
                Debug.LogError("Please fill in the ParticleAttachmentList", this);
            }
            ChangeAllObiAttachmentType(AttachmentType.Dynamic);
        }

        public void ChangeAllObiAttachmentType(AttachmentType _attachmentType)
        {
            if(!m_changeAttachmentType) return;
            foreach(ObiParticleAttachment _particleAttachment in m_particleAttachmentList)
            {
                ChangeObiAttachmentType(_particleAttachment, _attachmentType);
            }
        }
        public  void ChangeObiAttachmentType(ObiParticleAttachment _particleAttachment,AttachmentType _attachmentType)
        {
            switch(_attachmentType)
            {
                case AttachmentType.Static:
                    _particleAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
                    break;
                case AttachmentType.Dynamic:
                    _particleAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Dynamic;
                    break;
            }
        }
        #region Tools

        // Add Particle Attachment In Inspector By Context Menu or Button
        [ContextMenu("Add Particle Attachment In Inspector"), Button("ObiParticleAttachment AutoAdd")]
        public void AddParticleAttachmentInInspector()
        {
            if (m_particleAttachmentTransforms == null)
            {
                Debug.LogError("Please fill in the ParticleAttachmentTransforms", this);
                return;
            }

            List<ObiParticleAttachment> _AllobiParticleAttachments = new List<ObiParticleAttachment>();
            ObiParticleAttachment[] particleAttachments = null;
            List<ObiParticleAttachment> _targetParticleAttachments = new List<ObiParticleAttachment>();

            for (int i = 0; i < m_particleAttachmentTransforms.Count; i++)
            {
                particleAttachments = m_particleAttachmentTransforms[i].transform.GetComponents<ObiParticleAttachment>();
                _AllobiParticleAttachments.AddRange(particleAttachments);
            }

            if (_AllobiParticleAttachments.Count == 0)
            {
                Debug.LogError("Please select the ParticleAttachmentTransforms that includes' ObiParticleAttachment '", this);
                return;
            }


            for (int i = 0; i < _AllobiParticleAttachments.Count; i++)
            {
                if (_AllobiParticleAttachments[i].target == (m_attachmentTarget != null ? m_attachmentTarget : transform))
                {
                    _targetParticleAttachments.Add(_AllobiParticleAttachments[i]);
                }
            }
            if (_targetParticleAttachments.Count == 0)
            {
                Debug.LogError("Please check if AttachmentTarget is correct or matches ObiParticleAttachment", this);
                return;
            }
            m_particleAttachmentList.Clear();

            m_particleAttachmentList = _targetParticleAttachments;
        }

        #endregion
    }
    public enum AttachmentType
    {
        Static,
        Dynamic,
    }
}