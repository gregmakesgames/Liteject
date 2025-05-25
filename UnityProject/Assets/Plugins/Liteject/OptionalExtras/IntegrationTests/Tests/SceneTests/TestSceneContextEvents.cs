using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Liteject.Tests
{
    public class TestSceneContextEvents : SceneTestFixture
    {
        [UnityTest]
        public IEnumerator TestScene()
        {
            yield return LoadScene("TestSceneContextEvents");
            yield return new WaitForSeconds(1.0f);
        }
    }
}
