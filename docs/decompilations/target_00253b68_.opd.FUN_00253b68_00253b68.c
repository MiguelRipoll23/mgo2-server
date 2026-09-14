// original call TARGET 0x00253b68

undefined4 _opd_FUN_00253b68(uint *param_1,undefined2 param_2,undefined2 param_3,undefined4 param_4)

{
  undefined4 uVar1;
  
  uVar1 = 0xffffffff;
  if (param_1 != (uint *)0x0) {
    uVar1 = (*(code *)**(undefined4 **)(*(int *)(PTR_PTR_0121af28 + -0x7fe8) + (*param_1 & 0xf) * 4)
            )(param_1,param_2,param_3,param_4);
  }
  return uVar1;
}

