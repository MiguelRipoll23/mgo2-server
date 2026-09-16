// hook SITE 0x00280860

void _opd_FUN_002806f0(void)

{
  undefined4 *puVar1;
  undefined *puVar2;
  undefined4 uVar3;
  undefined4 uVar4;
  int iVar5;
  uint uVar6;
  uint uVar7;
  uint uVar8;
  byte local_f0;
  byte local_ef [3];
  uint local_ec;
  uint local_e8 [40];
  
  puVar2 = PTR_PTR_0121af7c;
  uVar3 = _opd_FUN_0027e2b0(0);
  uVar7 = 1;
  uVar8 = 0;
  do {
    while( true ) {
      iVar5 = _opd_FUN_0026d5a8(uVar8 & 0xff);
      if (iVar5 != 0) break;
      uVar8 = uVar8 + 1;
      if (0x17 < (int)uVar8) goto LAB_00280834;
    }
    uVar8 = uVar8 + 1;
    uVar4 = _opd_FUN_0027e2b0(uVar8);
    _opd_FUN_0027e480(uVar4,0x11f,1,&local_f0);
    _opd_FUN_0027e480(uVar4,0x120,1,local_ef);
    if (local_f0 != local_ef[0]) {
      _opd_FUN_0027e480(uVar4,0x114,4,&local_ec);
      uVar6 = uVar7 + 1;
      local_e8[uVar7 * 2] = local_ec;
      local_e8[uVar7 * 2 + 1] = (uint)local_f0;
      _opd_FUN_0027e578(uVar4,0x120,1,&local_f0);
      uVar7 = uVar6;
      if (0x11 < (int)uVar6) break;
    }
  } while ((int)uVar8 < 0x18);
LAB_00280834:
  _opd_FUN_0027e480(uVar3,0x59,1,&local_f0);
  puVar1 = *(undefined4 **)(puVar2 + -0x8000);
  iVar5 = _opd_FUN_00f0ea88(*puVar1,local_f0,uVar7 & 0xff,local_e8);
  if (iVar5 == 0) {
    puVar1[4] = puVar1[4] | 0x40000000;
    _opd_FUN_0026d990(0x94);
  }
  _opd_FUN_0026c9e0(puVar1 + 2);
  return;
}

