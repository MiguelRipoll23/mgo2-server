// original call TARGET 0x00f0ea88

undefined4 _opd_FUN_00f0ea88(int param_1,undefined4 param_2,uint param_3,int param_4)

{
  bool bVar1;
  undefined4 uVar2;
  int iVar3;
  undefined4 uVar4;
  undefined1 *puVar5;
  undefined4 uStack00000038;
  undefined1 auStack_500 [1216];
  
  if ((param_1 == 0) || (param_4 == 0)) {
    uVar4 = 0xffffffe8;
  }
  else {
    uStack00000038 = param_2;
    uVar2 = _opd_FUN_00f04688(param_1);
    iVar3 = _opd_FUN_00f045d0(param_1);
    uVar4 = 0xffffffdc;
    if (iVar3 != 0) {
      iVar3 = 1;
      _opd_FUN_00f2e540(auStack_500);
      _opd_FUN_00f2e4e8(auStack_500,0x4398);
      puVar5 = (undefined1 *)&stack0x00000038;
      while( true ) {
        _opd_FUN_00f2df64(auStack_500,puVar5);
        bVar1 = (int)(param_3 & 0xff) <= iVar3;
        iVar3 = iVar3 + 1;
        if (bVar1) break;
        _opd_FUN_00f2df64(auStack_500,param_4 + 8);
        puVar5 = (undefined1 *)(param_4 + 0xc);
        param_4 = param_4 + 8;
      }
      _opd_FUN_00f2ddd0(auStack_500);
      iVar3 = _opd_FUN_00f00a00(auStack_500,uVar2);
      uVar4 = 0xffffffc3;
      if (iVar3 == 0) {
        _opd_FUN_00efeb18(param_1,0x30,1);
        uVar4 = 0;
      }
    }
  }
  return uVar4;
}

