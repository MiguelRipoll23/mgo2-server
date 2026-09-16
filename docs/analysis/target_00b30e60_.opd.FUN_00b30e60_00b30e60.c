// original call TARGET 0x00b30e60

void _opd_FUN_00b30e60(undefined4 param_1,undefined4 param_2,char *param_3,int param_4)

{
  undefined *puVar1;
  int iVar2;
  undefined4 uVar3;
  
  puVar1 = PTR_DAT_0121bd24;
  iVar2 = _opd_FUN_00b18560();
  if (iVar2 == 0) {
    if ((param_3 == (char *)0x0) || (*param_3 == '\0')) {
      uVar3 = _opd_FUN_00242bf0(param_1,param_2);
      _opd_FUN_00245738(param_1,uVar3,*(undefined4 *)(puVar1 + -0x7fcc));
    }
    else {
      uVar3 = _opd_FUN_00242bf0(param_1,param_2);
      _opd_FUN_00245738(param_1,uVar3,param_3);
    }
    if (param_4 != 0) {
      uVar3 = _opd_FUN_00242bf0(param_1,param_2);
      _opd_FUN_00249d40(param_1,param_4,uVar3);
    }
  }
  return;
}

