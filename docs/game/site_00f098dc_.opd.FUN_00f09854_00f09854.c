// hook SITE 0x00f098dc

undefined4 _opd_FUN_00f09854(int param_1)

{
  bool bVar1;
  bool bVar2;
  uint uVar3;
  int *piVar4;
  int iVar5;
  int iVar6;
  undefined4 uVar7;
  byte local_50;
  byte local_4f [3];
  int local_4c;
  byte local_48 [4];
  undefined4 local_44 [7];
  
  uVar7 = 0xffffffe8;
  if (param_1 != 0) {
    piVar4 = (int *)_opd_FUN_00f04920(param_1);
    uVar7 = 0xffffffba;
    if (*piVar4 == 0x4124) {
      iVar6 = 0;
      _opd_FUN_00fa4d48(&UNK_00018618 + param_1,0,0xc00);
      _opd_FUN_00f2ddec(piVar4);
      iVar5 = _opd_FUN_00f2e20c(piVar4,&local_4c);
      if (iVar5 == 0) {
        while( true ) {
          bVar1 = local_4c <= iVar6;
          bVar2 = iVar6 == 0x100;
          iVar6 = iVar6 + 1;
          if ((bVar1) || (bVar2)) break;
          iVar5 = _opd_FUN_00f2e134(piVar4,local_48);
          if ((iVar5 != 0) || (iVar5 = _opd_FUN_00f2e280(piVar4,local_44), iVar5 != 0))
          goto LAB_00f09a20;
          iVar5 = (uint)local_48[0] * 0xc + 0x2f00 + param_1;
          (&DAT_00015710)[iVar5 + 8] = local_48[0];
          *(undefined4 *)(&DAT_00015710 + iVar5 + 0x10) = 0;
          *(undefined4 *)(&DAT_00015710 + iVar5 + 0xc) = local_44[0];
        }
        iVar5 = 0;
        do {
          iVar6 = _opd_FUN_00f2e134(piVar4,&local_50);
          if (iVar6 != 0) goto LAB_00f09a20;
          iVar6 = _opd_FUN_00f2e134(piVar4,local_4f);
          bVar1 = iVar5 != 0xf;
          iVar5 = iVar5 + 1;
          if (iVar6 != 0) goto LAB_00f09a20;
          uVar3 = 1 << (local_4f[0] & 0x3f);
          if ((local_4f[0] < 0x20) &&
             (iVar6 = (uint)local_50 * 0xc + 0x2f00 + param_1,
             (uVar3 & *(uint *)(&DAT_00015710 + iVar6 + 0xc)) != 0)) {
            *(uint *)(&DAT_00015710 + iVar6 + 0x10) =
                 *(uint *)(&DAT_00015710 + iVar6 + 0x10) | uVar3;
          }
        } while (bVar1);
        _opd_FUN_00f2de00(piVar4);
        uVar7 = 0;
      }
      else {
LAB_00f09a20:
        uVar7 = 0xffffffb9;
      }
    }
  }
  return uVar7;
}

