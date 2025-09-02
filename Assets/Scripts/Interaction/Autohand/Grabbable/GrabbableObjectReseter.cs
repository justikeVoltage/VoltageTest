/****************************************************
    功能：辅助抓取物品复位，依赖Grabbable或继承自Grabbable的脚本
    作者：ZZQ
    创建日期：#2025/02/20#
    修改人：ZZQ
    修改日期：#2025/02/20#
    修改内容：
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using Autohand;
using UnityEngine;
using UnityEngine.Events;

namespace Voltage
{
    public class GrabbableObjectReseter : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField]private Transform originalTransform;

        [Space,Header("Events")]
        [SerializeField]private UnityEvent m_afterPosReset=null;
        private Vector3 originalPosition; // Store the original position of the object
        private Quaternion originalRotation; // Store the original rotation of the object
        private Grabbable grab;
        private Hand hand1;
        
        private void Start()
        {
            originalPosition = transform.position; // Initialize the original position when the script starts
            originalRotation = transform.rotation;
            grab = GetComponent<Grabbable>();
            grab.OnReleaseEvent += OnReleaseGrabbable;
            CreateGameObjectPlacePoint(); // Create the place point for the object
        }
        void OnDisable()
        {
            if(grab!=null)
            grab.OnReleaseEvent -= OnReleaseGrabbable;
        }

        public void OnGrabbable(Hand hand, Grabbable grab)
        {
            //this.transform.GetComponent<Rigidbody>().isKinematic = false; // Make the object kinematic when grabbed
            //originalPosition = transform.position; // Set the original position when the object is grabbed
        }
        public void OnReleaseGrabbable(Hand hand, Grabbable grab)
        {
            //释放时判断Grabbable是否被另一只手抓住
            if (!grab.BeingGrabbed())
            {
                Debug.Log("Object Released,and beinggrabbed is : "+grab.BeingGrabbed());
                if (originalTransform == null)
                {
                    transform.position = originalPosition; // Reset the position when the object is released
                    transform.rotation = originalRotation; // Reset the rotation when the object is released
                }
                else
                {
                    transform.position = originalTransform.position; // Reset the position when the object is released
                    transform.rotation = originalTransform.rotation; // Reset the rotation when the object is released
                }
                m_afterPosReset?.Invoke();
                //Program executed after reset

            }
        }
        private void CreateGameObjectPlacePoint()
        {
            GameObject placePoint = new GameObject("PlacePointer"); // Create a new game object for the place point
            if (originalTransform==null)
            {
                placePoint.transform.position = transform.position; // Set the position of the place point to the same position as the object
                placePoint.transform.rotation = transform.rotation; // Set the rotation of the place point to the same rotation as the object
            }
            else
            {
                placePoint.transform.position = originalTransform.position; // Set the position of the place point to the same position as the original transform
                placePoint.transform.rotation = originalTransform.rotation; // Set the rotation of the place point to the same rotation as the original transform
            }
            
            placePoint.transform.localScale = Vector3.one; // Set the scale of the place point to 1
            placePoint.transform.parent = transform.parent; // Set the parent of the place point to the object
            Autohand.PlacePoint PlacePointer=  placePoint.AddComponent<Autohand.PlacePoint>(); // Add the PlacePoint script to the place point game object
            PlacePointer.shapeType = PlacePointShape.Box; // Set the shape of the place point to sphere
            PlacePointer.placeSize = new Vector3(0.3f, 0.4f, 0.1f); // Set the size of the place point to 1
            PlacePointer.shapeOffset = Vector3.zero; // Set the offset of the place point to 0
            PlacePointer.onlyAllows = new List<Grabbable>(); // Create a list of grabbable objects that can be placed on the place point
            PlacePointer.dontAllows = new List<Grabbable>(); // Create a list of grabbable objects that cannot be placed on the place point
            PlacePointer.placeNames = new string[0]; // Create a list of place names for the place point
            PlacePointer.blacklistNames = new string[0];  // Create a list of blacklisted place names for the place point
            PlacePointer.onlyAllows.Add(this.gameObject.GetComponent<Grabbable>()); // Set the grabbable object of the place point to the current object
            PlacePointer.makePlacedKinematic = true; // Make the placed object kinematic
            PlacePointer.heldPlaceOnly = false; // Allow the object to be placed anywhere in the scene
        }

    }
}