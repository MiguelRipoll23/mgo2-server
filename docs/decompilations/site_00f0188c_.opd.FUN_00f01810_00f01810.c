// hook SITE 0x00f0188c

undefined4 _opd_FUN_00f01810(int param_1,uint param_2)

{
  int iVar1;
  undefined4 uVar2;
  undefined1 auStack_4c0 [1176];
  
  if ((param_1 == 0) || (2 < param_2)) {
    return 0xffffffe8;
  }
  iVar1 = _opd_FUN_00f0168c(param_1,param_2);
  if (iVar1 == 0) {
    return 0xffffffdc;
  }
  _opd_FUN_00f2e540(auStack_4c0);
  _opd_FUN_00f2e4e8(auStack_4c0,5);
  _opd_FUN_00f2ddd0(auStack_4c0);
  uVar2 = _opd_FUN_00f01668(param_1,param_2);
  iVar1 = _opd_FUN_00f00a00(auStack_4c0,uVar2);
  if (iVar1 != 0) {
    return 0xffffffc3;
  }
  _opd_FUN_00efeb18(param_1,param_2 + 7,1);
  return 0;
}

