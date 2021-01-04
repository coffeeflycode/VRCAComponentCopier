using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using VRC.Core;

public class VRCAComponentCopier : EditorWindow
{
    GameObject objectCopyFrom;
    GameObject objectCopyTo;

    private string stringStatus = "";
    private string stringCopyFrom = "Copy from:";
    private string stringCopyTo = "Copy to:";
    private string stringCopyPipeline = "Copy Pipeline Manager";
    private string stringCopyPipelineTooltip = "Copying the Pipeline Manager will have the new model upload with the same name and details as the previous. Uncheck to upload as fresh model.";
    private string stringExistingAvatarDescriptorTitle = "Object copying to already has Avatar Descriptor";
    private string stringExistingAvatarDescriptorMessage = "Are you sure you want to copy components? If you have already been copied components, doing it again may take a very long time, or fail to copy.";
    private string stringExistingAvatarDescriptorOk = "Copy";
    private string stringExistingAvatarDescriptorCancel = "Cancel";
    private string stringEmptyTitle = "Error";
    private string stringEmptyCopyFrom = "There is no object to copy from.";
    private string stringEmptyCopyTo = "There is no object to copy to.";

    private bool isCopyPipelineAllowed = true;
    private bool isCopyCanceled = false;
    private bool isNullCopy = false;

    private Transform[] transformsCopyTo;
    private DynamicBoneCollider[] collidersCopyTo;
    private SkinnedMeshRenderer[] meshRenderersCopyTo;

    [MenuItem("VRChat SDK/VRCA Component Copier")]
    public static void ShowWindow()
    {
        GetWindow(typeof(VRCAComponentCopier)); //GetWindow is a method ineherited from the EditorWindow class
    }

    private void OnGUI()
    {
        GUILayout.Label("Copy components from one object, and all its children, to another object. Children of object must have the same name for copmonents to be copied.", EditorStyles.wordWrappedLabel);

        // Keep string empty until both fields filled, then display only once
        EditorGUI.BeginChangeCheck();
        objectCopyFrom = EditorGUILayout.ObjectField(stringCopyFrom, objectCopyFrom, typeof(GameObject), true) as GameObject;
        objectCopyTo = EditorGUILayout.ObjectField(stringCopyTo, objectCopyTo, typeof(GameObject), true) as GameObject;
        if (EditorGUI.EndChangeCheck() && objectCopyFrom != null && objectCopyTo != null)
        {
            stringStatus = "Ready";
        }

        isCopyPipelineAllowed = EditorGUILayout.Toggle(new GUIContent(stringCopyPipeline, stringCopyPipelineTooltip), isCopyPipelineAllowed);

        GUILayout.Space(20f);

        if (GUILayout.Button("Copy"))
        {
            CopyWithDelay();
        }

        GUILayout.Label(stringStatus, EditorStyles.centeredGreyMiniLabel);
    }

    async void CopyWithDelay()
    {
        isCopyCanceled = false;
        isNullCopy = false;

        // Adding 1 second delay to easily see when work starts and finishes
        CopyObject();
        if (isNullCopy)
        {
            stringStatus = "";
        }
        else if (isCopyCanceled)
        {
            stringStatus = "Canceled";
        }
        else
        {
            await Task.Delay(500);
            stringStatus = "Done";
        }
    }

    private void CopyObject()
    {
        // Display error if there is no object
        if (objectCopyFrom == null)
        {
            EditorUtility.DisplayDialog(stringEmptyTitle, stringEmptyCopyFrom, "Ok");
            isNullCopy = true;
            return;
        }
        if (objectCopyTo == null)
        {
            EditorUtility.DisplayDialog(stringEmptyTitle, stringEmptyCopyTo, "Ok");
            isNullCopy = true;
            return;
        }

        // Check if object already has Avatar Descriptor, to alert to possibly copying again
        VRCAvatarDescriptor avatarDescriptorCheck = objectCopyTo.GetComponentInChildren<VRCAvatarDescriptor>();
        if (avatarDescriptorCheck != null)
        {
            // If true then continue, else return
            if (!EditorUtility.DisplayDialog(stringExistingAvatarDescriptorTitle, stringExistingAvatarDescriptorMessage, stringExistingAvatarDescriptorOk, stringExistingAvatarDescriptorCancel))
            {
                isCopyCanceled = true;
                return;
            }
        }

        // Get all transforms from the object copying to
        transformsCopyTo = objectCopyTo.GetComponentsInChildren<Transform>();

        // Get all SkinnedMeshRenderers from the object copying to
        meshRenderersCopyTo = objectCopyTo.GetComponentsInChildren<SkinnedMeshRenderer>();

        // Copy Transforms      
        objectCopyTo.transform.position = objectCopyFrom.transform.position;
        objectCopyTo.transform.localScale = objectCopyFrom.transform.localScale;

        // Crawl through parent and then children, copying components if they dont exist already
        stringStatus = "Copying " + objectCopyFrom.GetComponentsInChildren(typeof(Component)).Length + " components...";
        CrawlHierarchy(objectCopyFrom, objectCopyTo);

        // Get all dynamic bone colliders from the object copying to, now that they have been copied over
        collidersCopyTo = objectCopyTo.GetComponentsInChildren<DynamicBoneCollider>();

        // Update references in all gameobjects of new model
        stringStatus = "Updating  " + objectCopyFrom.GetComponentsInChildren(typeof(Component)).Length + " components...";
        CrawlNewModel(objectCopyTo);
    }

    private void CrawlHierarchy(GameObject objectCurrentCopyFrom, GameObject objectCurrentCopyTo)
    {
        CopyComponents(objectCurrentCopyFrom, objectCurrentCopyTo);

        // Go through each child, recursively run copy for each child        
        for (int i = 0; i < objectCurrentCopyFrom.transform.childCount; i++)
        {
            GameObject objectNextCopyFrom = objectCurrentCopyFrom.transform.GetChild(i).gameObject;
            GameObject objectNextCopyTo = GetChildWithName(objectCurrentCopyTo, objectNextCopyFrom.transform.name);
            if (objectNextCopyTo != null)
            {
                CrawlHierarchy(objectNextCopyFrom, objectNextCopyTo);
            }
            else
            {
                Debug.LogWarning("Can't find " + objectNextCopyFrom.transform.name + " in new model.");
            }
        }
    }

    private void CrawlNewModel(GameObject objectCurrentCopyTo)
    {
        UpdateReferences(objectCurrentCopyTo);

        for (int i = 0; i < objectCurrentCopyTo.transform.childCount; i++)
        {
            GameObject objectNextCopyTo = objectCurrentCopyTo.transform.GetChild(i).gameObject;
            if (objectNextCopyTo != null)
            {
                CrawlNewModel(objectNextCopyTo);
            }
        }
    }

    private void UpdateReferences(GameObject objectCurrentCopyTo)
    {
        // Get new object's dynamic bone
        DynamicBone dBone = objectCurrentCopyTo.GetComponent<DynamicBone>();
        UpdateDynamicBones(dBone);

        // Get new object's avatar descriptor
        VRCAvatarDescriptor avatarDescriptor = objectCurrentCopyTo.GetComponent<VRCAvatarDescriptor>();
        UpdateAvatarDescriptor(avatarDescriptor);
    }

    private void UpdateDynamicBones(DynamicBone dBone)
    {
        if (dBone != null)
        {
            // Update the root if it exists 
            if (dBone.m_Root != null)
            {
                UpdateTransform(ref dBone.m_Root);
            }

            // Update any colliders, if they exist
            if (dBone.m_Colliders.Count > 0)
            {
                for (int i = 0; i < dBone.m_Colliders.Count; i++)
                {
                    if (dBone.m_Colliders[i] != null)
                    {
                        UpdateColliderList(dBone.m_Colliders, i);
                    }
                }
            }

            // Update any exclusions if they exist
            if (dBone.m_Exclusions.Count > 0)
            {
                for (int i = 0; i < dBone.m_Exclusions.Count; i++)
                {
                    if (dBone.m_Exclusions[i] != null)
                    {
                        UpdateTransformList(dBone.m_Exclusions, i);
                    }
                }
            }

            // Update reference to object if it exists
            if (dBone.m_ReferenceObject != null)
            {
                UpdateTransform(ref dBone.m_ReferenceObject);
            }
        }
    }

    private void UpdateAvatarDescriptor(VRCAvatarDescriptor avatarDescriptor)
    {
        if (avatarDescriptor != null)
        {
            if (avatarDescriptor.VisemeSkinnedMesh != null)
            {
                UpdateSkinnedMesh(ref avatarDescriptor.VisemeSkinnedMesh);
            }
            if (avatarDescriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
            {
                UpdateSkinnedMesh(ref avatarDescriptor.customEyeLookSettings.eyelidsSkinnedMesh);
            }
            if (avatarDescriptor.customEyeLookSettings.leftEye != null)
            {
                UpdateTransform(ref avatarDescriptor.customEyeLookSettings.leftEye);
            }
            if (avatarDescriptor.customEyeLookSettings.rightEye != null)
            {
                UpdateTransform(ref avatarDescriptor.customEyeLookSettings.rightEye);
            }
        }
    }

    private void CopyComponents(GameObject objectCurrentCopyFrom, GameObject ObjectCurrentCopyTo)
    {
        // look for components that don't exist.
        foreach (Component componentOriginal in objectCurrentCopyFrom.GetComponents<Component>())
        {
            if (componentOriginal.GetType() != typeof(Animator) &&
                componentOriginal.GetType() != typeof(Transform) &&
                componentOriginal.GetType() != typeof(SkinnedMeshRenderer))
            {
                if (componentOriginal.GetType() == typeof(PipelineManager))
                {
                    if (isCopyPipelineAllowed)
                    {
                        UnityEditorInternal.ComponentUtility.CopyComponent(componentOriginal);
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(ObjectCurrentCopyTo);
                    }
                }
                else
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(componentOriginal);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(ObjectCurrentCopyTo);
                }
            }
        }
    }

    #region Helper Methods

    private void UpdateSkinnedMesh(ref SkinnedMeshRenderer meshRendererToUpdate)
    {
        foreach (SkinnedMeshRenderer meshedRendererCopyTo in meshRenderersCopyTo)
        {
            if (meshRendererToUpdate.name == meshedRendererCopyTo.name)
            {
                meshRendererToUpdate = meshedRendererCopyTo;
            }
        }
    }

    private void UpdateTransform(ref Transform transformToUpdate)
    {
        // Look at names of each transform. If it matches the name of transformToUpdate, change transformToUpdate to new transform of same name.
        foreach (Transform transformCopyTo in transformsCopyTo)
        {
            if (transformToUpdate.name == transformCopyTo.name)
            {
                transformToUpdate = transformCopyTo;
            }
        }
    }

    private void UpdateTransformList(List<Transform> TransformsToUpdate, int intChangeIdx)
    {
        // Look at names of each transform. If it matches the name of transformToUpdate, change transformToUpdate to new transform of same name.
        foreach (Transform transformCopyTo in transformsCopyTo)
        {
            if (TransformsToUpdate[intChangeIdx].name == transformCopyTo.name)
            {
                TransformsToUpdate[intChangeIdx] = transformCopyTo;
            }
        }
    }

    private void UpdateColliderList(List<DynamicBoneColliderBase> collidersToUpdate, int intChangeIdx)
    {
        // Look at names of each collider. If it matches the name of colliderToUpdate, change colliderToUpdate to new name.
        foreach (DynamicBoneColliderBase colliderCopyTo in collidersCopyTo)
        {
            if (collidersToUpdate[intChangeIdx].name == colliderCopyTo.name)
            {
                collidersToUpdate[intChangeIdx] = colliderCopyTo;
            }
        }
    }
    private GameObject GetChildWithName(GameObject objectToSearchIn, string name)
    {
        Transform trans = objectToSearchIn.transform;
        Transform childTrans = trans.Find(name);
        if (childTrans != null)
        {
            return childTrans.gameObject;
        }
        else
        {
            return null;
        }
    }
    #endregion
}
