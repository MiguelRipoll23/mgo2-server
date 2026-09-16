// hook SITE 0x00275b34

undefined4 _opd_FUN_00275a90(undefined8 param_1,byte *param_2,undefined4 param_3)

{
  undefined2 uVar1;
  undefined4 uVar2;
  int iVar3;
  byte *pbVar4;
  byte *pbVar5;
  undefined1 local_40 [4];
  undefined1 auStack_3c [4];
  undefined1 local_38 [16];
  
  if ((*(uint *)(param_2 + 8) & 0x20000) != 0) {
    return 0xffffffff;
  }
  uVar2 = _opd_FUN_0027e2b0(*param_2 + 1);
  _opd_FUN_0027e480(uVar2,0x114,4,auStack_3c);
  _opd_FUN_0026cc10(param_3,param_2,1);
  _opd_FUN_0026cc10(param_3,auStack_3c,4);
  _opd_FUN_0026cc10(param_3,local_38,4);
  _opd_FUN_0026cc10(param_3,param_2 + 0x66,2);
  uVar1 = *(undefined2 *)(param_2 + 100);
  _opd_FUN_0026cc10(param_3,local_40,1);
  if ((byte)uVar1 != 0) {
    iVar3 = 0;
    pbVar5 = param_2 + 0x60;
    do {
      iVar3 = iVar3 + 1;
      _opd_FUN_0026cb98(param_3,pbVar5 + 8,4);
      pbVar4 = pbVar5 + 0xc;
      pbVar5 = pbVar5 + 6;
      _opd_FUN_0026cc10(param_3,pbVar4,2);
    } while (iVar3 < (int)(uint)(byte)uVar1);
  }
  _opd_FUN_0026cc10(param_3,param_2 + 0x58,4);
  _opd_FUN_0026cc10(param_3,param_2 + 0x5c,1);
  iVar3 = _opd_FUN_00f9dde8(param_2 + 0x24);
  _opd_FUN_0026cb98(param_3,param_2 + 0x24,iVar3 + 1);
  uVar2 = _opd_FUN_00f9dde8(param_2 + 0x40);
  _opd_FUN_0026cb98(param_3,param_2 + 0x40,uVar2);
  return 0;
}

