using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingPanel : MonoBehaviour
{
    public static LoadingPanel instance;
    private Animator animator;
    private void Awake()
    {
        animator = this.GetComponent<Animator>();
        
        if (!instance)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            //DesactiveLoadingPanel();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void ActiveLoadingPanel()
    {
        animator.Play("OnChain_transaction");
    }
    public void DesactiveLoadingPanel()
    {
        animator.Play("OnChain_transaction_finished");
    }
}

