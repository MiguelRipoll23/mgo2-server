// original call TARGET 0x00245968

undefined4 _opd_FUN_00245968(int param_1,uint *param_2,int param_3)

{
  char cVar1;
  uint uVar2;
  undefined1 *puVar3;
  char *pcVar4;
  uint uVar5;
  int iVar6;
  char cVar7;
  longlong lVar8;
  int iVar9;
  byte bVar11;
  ulonglong uVar10;
  char cVar12;
  
  if ((param_2 != (uint *)0x0) && ((*param_2 & 0x80000) != 0)) {
    iVar9 = 0;
    iVar6 = 0;
    if (param_3 == 0) {
      if (param_2[0xb] == 0) {
        return 0;
      }
      (*(code *)**(undefined4 **)(param_1 + 0x18))(param_2[0xb]);
      param_2[0xb] = 0;
      *param_2 = *param_2 | 0x40000000;
      *(ushort *)((int)param_2 + 0x16) =
           *(ushort *)((int)param_2 + 0x16) & 0xfe00 |
           (ushort)((((ulonglong)*(ushort *)((int)param_2 + 0x16) & 0xff) << 0x17) >> 0x10) >> 7;
      return 1;
    }
    while( true ) {
      while( true ) {
        while( true ) {
          pcVar4 = (char *)(param_3 + iVar9);
          cVar7 = *pcVar4;
          while (cVar7 != '\\') {
            if (cVar7 == '\0') goto LAB_00245ae4;
            iVar9 = iVar9 + 1;
            pcVar4 = (char *)(param_3 + iVar9);
            iVar6 = iVar6 + 1;
            cVar7 = *pcVar4;
          }
          cVar7 = pcVar4[1];
          if (((cVar7 != '\\') && (cVar7 != 'n')) && (cVar7 != 'r')) break;
          iVar9 = iVar9 + 2;
          iVar6 = iVar6 + 1;
        }
        if (cVar7 != 'x') break;
        iVar9 = iVar9 + 4;
        iVar6 = iVar6 + 1;
      }
      if (cVar7 == '0') break;
      iVar9 = iVar9 + 1;
      iVar6 = iVar6 + 1;
    }
LAB_00245ae4:
    uVar5 = (*(code *)**(undefined4 **)(param_1 + 0x14))(iVar6 + 2);
    iVar6 = 0;
    iVar9 = 0;
    if (uVar5 != 0) {
      do {
        while( true ) {
          while( true ) {
            while( true ) {
              while( true ) {
                pcVar4 = (char *)(param_3 + iVar6);
                cVar7 = *pcVar4;
                while (cVar7 != '\\') {
                  *(char *)(uVar5 + iVar9) = cVar7;
                  if (*pcVar4 == '\0') goto LAB_00245c18;
                  iVar6 = iVar6 + 1;
                  iVar9 = iVar9 + 1;
                  pcVar4 = (char *)(param_3 + iVar6);
                  cVar7 = *pcVar4;
                }
                cVar7 = pcVar4[1];
                if (cVar7 != '\\') break;
                puVar3 = (undefined1 *)(uVar5 + iVar9);
                iVar6 = iVar6 + 2;
                iVar9 = iVar9 + 1;
                *puVar3 = 0x5c;
              }
              if (cVar7 != 'n') break;
              puVar3 = (undefined1 *)(uVar5 + iVar9);
              iVar6 = iVar6 + 2;
              iVar9 = iVar9 + 1;
              *puVar3 = 10;
            }
            if (cVar7 != 'r') break;
            puVar3 = (undefined1 *)(uVar5 + iVar9);
            iVar6 = iVar6 + 2;
            iVar9 = iVar9 + 1;
            *puVar3 = 0xd;
          }
          if (cVar7 == 'x') break;
          if (cVar7 == '0') {
            *(undefined1 *)(uVar5 + iVar9) = 0;
LAB_00245c18:
            uVar2 = param_2[0xb];
            if (uVar2 != 0) {
              iVar6 = _opd_FUN_00f9db58(uVar2,uVar5);
              if (iVar6 == 0) {
                (*(code *)**(undefined4 **)(param_1 + 0x18))(uVar5);
                return 0;
              }
              (*(code *)**(undefined4 **)(param_1 + 0x18))(uVar2);
            }
            param_2[0xb] = uVar5;
            *param_2 = *param_2 | 0x40000000;
            *(ushort *)((int)param_2 + 0x16) =
                 *(ushort *)((int)param_2 + 0x16) & 0xfe00 |
                 (ushort)((((ulonglong)*(ushort *)((int)param_2 + 0x16) & 0xff) << 0x17) >> 0x10) >>
                 7;
            return 1;
          }
          puVar3 = (undefined1 *)(uVar5 + iVar9);
          iVar6 = iVar6 + 1;
          iVar9 = iVar9 + 1;
          *puVar3 = 0x5c;
        }
        bVar11 = pcVar4[2] - 0x30;
        lVar8 = (longlong)pcVar4[2];
        if (bVar11 < 0x37) {
          uVar10 = 1L << ((longlong)(char)bVar11 & 0x7fU);
          cVar7 = (char)((lVar8 - 0x30U & 0xffffffff) << 4);
          if ((((uVar10 & 0x3ff) == 0) &&
              (cVar7 = (char)((lVar8 - 0x37U & 0xffffffff) << 4), (uVar10 & 0x7e0000) == 0)) &&
             (cVar7 = (char)((lVar8 - 0x57U & 0xffffffff) << 4), (uVar10 & 0x7e000000000000) == 0))
          goto LAB_00245d78;
        }
        else {
LAB_00245d78:
          cVar7 = '\0';
        }
        cVar1 = pcVar4[3];
        if ((byte)(cVar1 - 0x30U) < 0x37) {
          cVar12 = cVar1 + -0x30;
          uVar10 = 1L << ((longlong)(char)(cVar1 - 0x30U) & 0x7fU);
          if ((((uVar10 & 0x3ff) == 0) && (cVar12 = cVar1 + -0x37, (uVar10 & 0x7e0000) == 0)) &&
             (cVar12 = cVar1 + -0x57, (uVar10 & 0x7e000000000000) == 0)) goto LAB_00245d70;
        }
        else {
LAB_00245d70:
          cVar12 = '\0';
        }
        pcVar4 = (char *)(uVar5 + iVar9);
        iVar6 = iVar6 + 4;
        iVar9 = iVar9 + 1;
        *pcVar4 = cVar12 + cVar7;
      } while( true );
    }
  }
  return 0xffffffff;
}

