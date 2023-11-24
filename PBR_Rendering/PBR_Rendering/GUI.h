void DrawGUI(float &slide,glm::vec3 lightPositon[], glm::vec3 lightColor[],std::string &LightText)
{
    ImGui::Begin("Menu");

    ImGui::SliderFloat("slide", &slide, 0.0f, 1.0f);

    if (ImGui::BeginCombo("Light", LightText.c_str()))
    {
        for (size_t i = 0; i < 4; i++)
        {
            if (ImGui::Selectable(std::to_string(i).c_str()))
            {
                LightText="light"+std::to_string(i);
            }
        }
        ImGui::EndCombo();
    }
    ImGui::End();
}